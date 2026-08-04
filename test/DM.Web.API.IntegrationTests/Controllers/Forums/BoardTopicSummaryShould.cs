using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Forum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Topic = DM.Domain.Forum.Features.Topics.Topic;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// A board carries a denormalized topic count and a "last topic" block. They used
/// to be raised on creation and adjusted nowhere else, so deleting a topic or
/// moving one between boards left the board claiming a count it did not have and
/// pointing at a topic that was no longer there — with no path back short of
/// editing the database by hand.
/// </summary>
public class BoardTopicSummaryShould : IntegrationTestBase
{
    public BoardTopicSummaryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CountTheTopicsCreatedInTheBoard()
    {
        var boardId = await CreateBoard();
        try
        {
            await CreateTopic(boardId, 0);
            var second = await CreateTopic(boardId, 1);

            var board = await ReadBoard(boardId);

            board.TopicsCount.Should().Be(2);
            board.LastTopicId.Should().Be(second.Id);
            board.LastTopicNumber.Should().Be(second.TopicNumber);
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task DropADeletedTopicFromTheCountAndFallBackToThePreviousOne()
    {
        var boardId = await CreateBoard();
        try
        {
            var first = await CreateTopic(boardId, 0);
            var second = await CreateTopic(boardId, 1);

            await DeleteTopic(second.Id);

            var board = await ReadBoard(boardId);

            board.TopicsCount.Should().Be(1);
            board.LastTopicId.Should().Be(first.Id);
            board.LastTopicTitle.Should().Be(first.Title);
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task ClearTheSummaryWhenTheLastTopicIsDeleted()
    {
        var boardId = await CreateBoard();
        try
        {
            var only = await CreateTopic(boardId, 0);

            await DeleteTopic(only.Id);

            var board = await ReadBoard(boardId);

            board.TopicsCount.Should().Be(0);
            board.LastTopicId.Should().BeNull();
            board.LastTopicNumber.Should().BeNull();
            board.LastTopicTitle.Should().BeNull();
            board.LastTopicAuthorId.Should().BeNull();
            board.LastTopicCreatedUtc.Should().BeNull();
        }
        finally
        {
            await DropBoard(boardId);
        }
    }

    [Fact]
    public async Task MoveTheSummaryWithTheTopicWhenItChangesBoard()
    {
        var source = await CreateBoard();
        var destination = await CreateBoard();
        try
        {
            var moved = await CreateTopic(source, 0);

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
                await repository.Update(new UpdateTopicEntity { TopicId = moved.Id }, destination);
            }

            var sourceBoard = await ReadBoard(source);
            var destinationBoard = await ReadBoard(destination);

            sourceBoard.TopicsCount.Should().Be(0);
            sourceBoard.LastTopicId.Should().BeNull();

            destinationBoard.TopicsCount.Should().Be(1);
            destinationBoard.LastTopicId.Should().Be(moved.Id);
        }
        finally
        {
            await DropBoard(source);
            await DropBoard(destination);
        }
    }

    /// <summary>
    /// The comment counter and the last-comment block are computed on read, so a
    /// new comment has to show up on the very next render.
    /// </summary>
    /// <remarks>
    /// They used to be columns that only the demo seeder ever wrote: in production
    /// the forum showed zero comments on every board forever, and "last activity"
    /// could never name a comment.
    /// </remarks>
    [Fact]
    public async Task CountCommentsOfTheBoardAsSoonAsTheyAppear()
    {
        var boardId = await CreateBoard();
        try
        {
            var topic = await CreateTopic(boardId, 0);

            var before = await ReadBoardSummary(boardId);
            before.CommentsCount.Should().Be(0);
            before.LastComment.Should().BeNull();

            await AddComment(topic.Id, "First");
            await AddComment(topic.Id, "Second");

            var after = await ReadBoardSummary(boardId);
            after.CommentsCount.Should().Be(2);
            after.LastComment.Should().NotBeNull();
            after.LastComment!.TopicId.Should().Be(topic.Id);
        }
        finally
        {
            await DropComments(boardId);
            await DropBoard(boardId);
        }
    }

    private async Task AddComment(Guid topicId, string text)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Set<DM.Infrastructure.Persistence.Entities.Shared.Comment>().Add(new()
        {
            CommentId = Guid.NewGuid(),
            EntityId = topicId,
            AuthorId = TestConstants.TestUserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Text = text,
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<DM.Domain.Forum.Features.Boards.Board> ReadBoardSummary(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<DM.Domain.Forum.Features.Boards.IBoardRepository>();
        var boards = await repository.SelectBoards(null);
        return boards.First(b => b.Id == boardId);
    }

    private async Task DropComments(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Comments" WHERE "EntityId" IN (SELECT "TopicId" FROM "Topics" WHERE "BoardId" = {0})""",
            boardId);
    }
    private async Task<Topic> CreateTopic(Guid boardId, int index)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
        return await repository.Create(
            new CreateTopicEntity { Title = $"Summary topic {index}", Text = "text" },
            TestConstants.TestUserId, boardId);
    }

    private async Task DeleteTopic(Guid topicId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
        await repository.Delete(topicId, TestConstants.TestUserId);
    }

    private async Task<Board> ReadBoard(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        return await dbContext.Boards.AsNoTracking().FirstAsync(b => b.BoardId == boardId);
    }

    private async Task<Guid> CreateBoard()
    {
        var boardId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Boards.Add(new Board
        {
            BoardId = boardId,
            Title = $"Summary board {boardId:N}",
            Alias = $"summary-{boardId:N}",
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.Guest,
        });
        await dbContext.SaveChangesAsync();
        return boardId;
    }

    private async Task DropBoard(Guid boardId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        // The board points back at its last topic, so the denormalized link has to
        // be released before the topics it references can go.
        await dbContext.Boards
            .Where(b => b.BoardId == boardId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.LastTopicId, (Guid?)null));
        await dbContext.Topics.IgnoreQueryFilters().Where(t => t.BoardId == boardId).ExecuteDeleteAsync();
        await dbContext.Boards.Where(b => b.BoardId == boardId).ExecuteDeleteAsync();
    }
}
