using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Forum search runs on generated tsvector columns rather than a separate search
/// store, and this is what that buys: Russian morphology, a title that outranks a
/// body, and visibility that is a join rather than a copy of the access rules
/// kept beside the text. The store it replaced could not do the last one — the
/// authorization it wrote into each document was assembled at index time, so a
/// board whose policy changed stayed wrong until something reindexed it.
/// </summary>
public class ForumSearchShould : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ForumSearchShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task MatchARussianWordInAnotherGrammaticalForm()
    {
        var boardId = await CreateBoard(BoardAccessPolicy.Guest);
        try
        {
            await CreateTopic(boardId, "Хроники ольмагара", "Ведущий собирает партию странников.");

            // "странников" is the genitive plural of "странник": a substring match
            // finds nothing here, the Russian dictionary finds it.
            var hits = await Search("странник");

            hits.Should().ContainSingle(h => h.TopicTitle == "Хроники ольмагара");

            // The preview comes from the same dictionary as the match, so it marks
            // the inflected form rather than the word that was typed.
            var snippet = hits.Single().Snippet;
            snippet.Should().NotBeEmpty("a hit without a preview gives the reader nothing to read");
            snippet.Should().Contain(segment => segment.IsMatch,
                "the preview says which run of it is the match, and a preview that marks " +
                "nothing is a highlight the client cannot draw");
            string.Concat(snippet.Select(segment => segment.Text))
                .Should().Contain("странник",
                    "the run that was matched is the inflected form out of the text, not the " +
                    "query that found it");
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task RankATitleMatchAboveABodyMatch()
    {
        var boardId = await CreateBoard(BoardAccessPolicy.Guest);
        try
        {
            await CreateTopic(boardId, "Обычная тема", "Здесь про ольмагара в тексте.", 1);
            await CreateTopic(boardId, "Ольмагара ищут все", "Здесь про что-то другое.", 2);

            var hits = await Search("ольмагар");

            hits.Should().HaveCount(2);
            hits[0].TopicTitle.Should().Be("Ольмагара ищут все",
                "the title carries weight A and the body weight B");
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task FindACommentAndPointAtItsTopic()
    {
        var boardId = await CreateBoard(BoardAccessPolicy.Guest);
        try
        {
            var topicId = await CreateTopic(boardId, "Тема без совпадений", "Ничего интересного.");
            var commentId = await AddComment(topicId, "А вот и ольмагар в комментарии.");

            var hits = await Search("ольмагар");

            var hit = hits.Should().ContainSingle().Subject;
            hit.EntityType.Should().Be("comment");
            hit.Id.Should().Be(commentId);
            hit.TopicId.Should().Be(topicId);
            hit.TopicTitle.Should().Be("Тема без совпадений");
        }
        finally
        {
            await PostTestHelper.DropComments(DatabaseFixture, boardId);
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task IgnoreCommentsThatAreNotOnAForumTopic()
    {
        var boardId = await CreateBoard(BoardAccessPolicy.Guest);
        try
        {
            // Comment.EntityId is polymorphic and the tsvector column indexes every
            // kind of comment, so a game comment is in the index too. Only the join
            // to Topics keeps it out of forum results.
            await AddComment(TestConstants.TestGameId, "Ольмагар в комментарии к игре.");

            var hits = await Search("ольмагар");

            hits.Should().BeEmpty();
        }
        finally
        {
            await DropCommentsOn(TestConstants.TestGameId);
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task HideABoardTheCallerMayNotRead()
    {
        var boardId = await CreateBoard(BoardAccessPolicy.Moderator);
        try
        {
            await CreateTopic(boardId, "Служебная тема", "Ольмагар только для модераторов.");

            var anonymous = await Search("ольмагар");
            anonymous.Should().BeEmpty("a guest holds no bit the board admits");

            var moderator = await Search("ольмагар", CustomWebApplicationFactory.CreateAdminUser());
            moderator.Should().ContainSingle(h => h.TopicTitle == "Служебная тема");
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task RejectAnEmptyQuery()
    {
        var response = await Client.GetAsync("/v1/search/forum?search=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<ForumSearchRow[]> Search(string query, DM.Domain.Core.Dto.GeneralUser? asUser = null)
    {
        var url = $"/v1/search/forum?search={Uri.EscapeDataString(query)}";
        var response = asUser is null
            ? await Client.GetAsync(url)
            : await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, asUser));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ForumSearchEnvelope>(JsonOptions);
        return payload!.Resources.ToArray();
    }

    private async Task<Guid> CreateBoard(BoardAccessPolicy viewPolicy)
    {
        var boardId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Boards.Add(new Board
        {
            BoardId = boardId,
            Title = $"Search board {boardId:N}",
            Alias = $"search-{boardId:N}",
            Order = 0,
            ViewPolicy = viewPolicy,
            CreateTopicPolicy = viewPolicy,
        });
        await dbContext.SaveChangesAsync();
        return boardId;
    }

    // TopicNumber is unique per board, so every topic a test adds needs its own.
    private async Task<Guid> CreateTopic(Guid boardId, string title, string text, int topicNumber = 1)
    {
        var topicId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Topics.Add(new Topic
        {
            TopicId = topicId,
            BoardId = boardId,
            AuthorId = TestConstants.TestUserId,
            Title = title,
            Text = text,
            TopicNumber = topicNumber,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return topicId;
    }

    private async Task<Guid> AddComment(Guid entityId, string text)
    {
        var commentId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Set<DbComment>().Add(new DbComment
        {
            CommentId = commentId,
            EntityId = entityId,
            AuthorId = TestConstants.TestUserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Text = text,
        });
        await dbContext.SaveChangesAsync();
        return commentId;
    }


    private async Task DropCommentsOn(Guid entityId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Comments" WHERE "EntityId" = {0}""", entityId);
    }

    private async Task DropBoard(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Topics" WHERE "BoardId" = {0}""", boardId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Boards" WHERE "BoardId" = {0}""", boardId);
    }

    private class ForumSearchEnvelope
    {
        public IEnumerable<ForumSearchRow> Resources { get; set; } = [];
    }

    private class ForumSearchRow
    {
        public string EntityType { get; set; } = "";
        public Guid Id { get; set; }
        public Guid TopicId { get; set; }
        public string TopicTitle { get; set; } = "";

        /// <summary>
        /// Runs of the preview, not a marked-up string: the database marks the
        /// match, and a string carrying those marks would have to be rendered as
        /// markup by whoever read it. Declared as a string here, this row simply
        /// failed to deserialise - which is a contract change nothing else in the
        /// suite would have noticed.
        /// </summary>
        public IReadOnlyList<SnippetSegment> Snippet { get; set; } = [];
    }

    /// <summary>One run of a preview, as the wire carries it.</summary>
    private class SnippetSegment
    {
        public string Text { get; set; } = "";
        public bool IsMatch { get; set; }
    }
}
