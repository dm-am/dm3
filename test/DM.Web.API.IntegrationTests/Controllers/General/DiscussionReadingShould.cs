using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DM.Web.API.IntegrationTests.Helpers;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBlogBlacklist = DM.Infrastructure.Persistence.Entities.Blog.BlogBlacklist;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbGameBlacklist = DM.Infrastructure.Persistence.Entities.Game.Links.GameBlacklist;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Every discussion is read by everybody who sees the place it is in, the guest
/// included, end to end: the discussion of a game in either of its modes, of a
/// blog, under a publication and of a topic.
/// </summary>
/// <remarks>
/// Comments cannot be switched off and are read by everybody, always: reading is
/// closed only by a closed room of a game and by what is private. A blacklist and
/// a ban forbid writing, in particular places, and never reading. So every reader below reads every discussion: the guest,
/// a stranger, the person on the place's blacklist and on its owner's personal
/// one, and a stranger under either ban.
///
/// The mode of a game's discussion decides who may write and nothing else, so no
/// mode closes reading: a request that asks for one is refused.
/// </remarks>
public class DiscussionReadingShould : IntegrationTestBase
{
    public DiscussionReadingShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>Who reads.</summary>
    public enum Reader
    {
        Guest,
        Stranger,
        Blacklisted,
        UnderOrdinaryBan,
        UnderFullBan
    }

    public static TheoryData<Reader> Readers => new()
    {
        Reader.Guest, Reader.Stranger, Reader.Blacklisted, Reader.UnderOrdinaryBan, Reader.UnderFullBan
    };

    [Theory]
    [MemberData(nameof(Readers))]
    public async Task LetEveryReaderReadEveryDiscussion(Reader reader)
    {
        var world = await SeedAsync();
        var caller = reader switch
        {
            Reader.Guest => null,
            Reader.Blacklisted => world.Blacklisted,
            _ => world.Stranger
        };
        var policy = reader switch
        {
            Reader.UnderOrdinaryBan => AccessPolicy.DemocraticBan,
            Reader.UnderFullBan => AccessPolicy.FullBan,
            _ => AccessPolicy.NotSpecified
        };

        var reads = new (string Place, string Listing, string Single, Guid Comment)[]
        {
            ("a public game", $"/v1/games/{world.PublicGame}/comments", "/v1/games/comments",
                world.PublicGameComment),
            ("a game only its people write in", $"/v1/games/{world.ReadOnlyGame}/comments", "/v1/games/comments",
                world.ReadOnlyGameComment),
            ("a blog", $"/v1/blogs/{world.Blog}/comments", "/v1/blogs/comments", world.BlogComment),
            ("a publication", $"/v1/publications/{world.Publication}/comments", "/v1/publications/comments",
                world.PublicationComment),
            ("a topic", $"/v1/topics/{world.Topic}/comments", "/v1/forum/comments", world.TopicComment)
        };

        using var scope = new AssertionScope();
        foreach (var (place, listing, single, comment) in reads)
        {
            var listed = await GetAsync(listing, caller, policy);
            listed.Status.Should().Be(HttpStatusCode.OK, $"{reader} reads the discussion of {place}: {listed.Body}");
            if (listed.Status == HttpStatusCode.OK)
            {
                CommentIds(listed.Body).Should().Contain(comment, $"{reader} reads the comments of {place}");
            }

            (await GetAsync($"{single}/{comment}", caller, policy)).Status.Should().Be(HttpStatusCode.OK,
                $"{reader} reads a comment of {place} on its own");
        }

        foreach (var game in new[] { world.PublicGame, world.ReadOnlyGame })
        {
            (await GetAsync($"/v1/games/{game}/comments/first-unread", caller, policy)).Status
                .Should().Be(HttpStatusCode.OK, $"{reader} is taken to the first unread comment of a game");
        }
    }

    /// <summary>
    /// A mode closing reading to everybody but the players is no mode: asked for
    /// by the name "Private" or by the number 2 it is refused, and a mode that is
    /// one is taken.
    /// </summary>
    [Fact]
    public async Task RefuseADiscussionModeClosingReading()
    {
        var world = await SeedAsync();
        var url = $"/v1/games/{world.ReadOnlyGame}/details";

        var byName = await PatchAsync(url, world.Master, "Private");
        byName.Status.Should().Be(HttpStatusCode.BadRequest, "no discussion is closed to its readers: {0}", byName.Body);
        var byNumber = await PatchAsync(url, world.Master, 2);
        byNumber.Status.Should().Be(HttpStatusCode.BadRequest,
            "2 is no mode either: {0}", byNumber.Body);
        var taken = await PatchAsync(url, world.Master, "Public");
        taken.Status.Should().Be(HttpStatusCode.OK, taken.Body);

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Games.Where(g => g.GameId == world.ReadOnlyGame).Select(g => g.CommentsAccessMode).SingleAsync())
            .Should().Be(CommentsAccessMode.Public);
    }

    private sealed record Answer(HttpStatusCode Status, string Body);

    private async Task<Answer> GetAsync(string url, GeneralUser? caller, AccessPolicy policy)
    {
        var request = caller == null
            ? new HttpRequestMessage(HttpMethod.Get, url)
            : CreateAuthenticatedRequest(HttpMethod.Get, url, caller);
        if (caller != null && policy != AccessPolicy.NotSpecified)
        {
            request.Headers.Add(TestAuthenticationMiddleware.TestUserAccessPolicyHeader, policy.ToString());
        }

        var response = await Client.SendAsync(request);
        return new Answer(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<Answer> PatchAsync(string url, GeneralUser caller, object mode)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Patch, url, caller);
        request.Content = JsonContent.Create(new { privacySettings = new { commentariesAccess = mode } });
        var response = await Client.SendAsync(request);
        return new Answer(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static Guid[] CommentIds(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("resources").EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid())
            .ToArray();
    }

    // ═══ SEEDING ═══

    private sealed record World(
        GeneralUser Master,
        GeneralUser Stranger,
        GeneralUser Blacklisted,
        Guid PublicGame,
        Guid PublicGameComment,
        Guid ReadOnlyGame,
        Guid ReadOnlyGameComment,
        Guid Blog,
        Guid BlogComment,
        Guid Publication,
        Guid PublicationComment,
        Guid Topic,
        Guid TopicComment);

    /// <summary>
    /// A master who leads a public game and a game only its people write in, keeps
    /// a blog with a publication and opened a topic in a board of his own, each
    /// with a comment of his; a stranger; and a user both games and the blog
    /// blacklisted, whom the master also keeps on his personal list. Fresh
    /// identifiers every time: the fixture database is shared and its usernames
    /// and public ids are uniquely indexed.
    /// </summary>
    private async Task<World> SeedAsync()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var master = AddUser(db, "drm");
        var stranger = AddUser(db, "drs");
        var blacklisted = AddUser(db, "drb");

        var publicGame = AddGame(db, master.UserId, CommentsAccessMode.Public);
        var readOnlyGame = AddGame(db, master.UserId, CommentsAccessMode.Readonly);
        var blogId = Guid.NewGuid();
        db.Blogs.Add(new DbBlog
        {
            BlogId = blogId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = master.UserId,
            Title = "Блог с обсуждением",
            Description = "Блог для проверки чтения",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Public,
            CreatedUtc = now.AddDays(-2),
            ActivatedUtc = now.AddDays(-1)
        });
        var publicationId = Guid.NewGuid();
        db.Publications.Add(new DbPublication
        {
            PublicationId = publicationId,
            BlogId = blogId,
            PublicationNumber = 1,
            AuthorId = master.UserId,
            Title = "Публикация с обсуждением",
            Content = "Текст",
            Preview = "Текст",
            IsPublished = true,
            PublishedUtc = now.AddHours(-3),
            CreatedUtc = now.AddHours(-3)
        });
        var boardId = Guid.NewGuid();
        db.Boards.Add(new DbBoard
        {
            BoardId = boardId,
            Title = $"Reading board {boardId:N}",
            Alias = $"drd-{boardId:N}",
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.RegularUser
        });
        var topicId = Guid.NewGuid();
        db.Topics.Add(new DbTopic
        {
            TopicId = topicId,
            BoardId = boardId,
            AuthorId = master.UserId,
            Title = "Топик с обсуждением",
            Text = "Текст топика",
            TopicNumber = 1,
            CreatedUtc = now.AddHours(-3)
        });

        foreach (var game in new[] { publicGame, readOnlyGame })
        {
            db.GameBlacklists.Add(new DbGameBlacklist
            {
                EntryId = Guid.NewGuid(),
                GameId = game,
                BlockedUserId = blacklisted.UserId,
                BlockedByUserId = master.UserId,
                CreatedUtc = now
            });
        }

        db.BlogBlacklists.Add(new DbBlogBlacklist
        {
            EntryId = Guid.NewGuid(),
            BlogId = blogId,
            BlockedUserId = blacklisted.UserId,
            BlockedByUserId = master.UserId,
            CreatedUtc = now
        });
        db.UserBlacklists.Add(new DbUserBlacklist
        {
            EntryId = Guid.NewGuid(),
            OwnerId = master.UserId,
            BlockedUserId = blacklisted.UserId,
            CreatedUtc = now
        });

        var comments = new[] { publicGame, readOnlyGame, blogId, publicationId, topicId }
            .Select(entityId => AddComment(db, entityId, master.UserId, now))
            .ToArray();
        await db.SaveChangesAsync();

        return new World(Caller(master), Caller(stranger), Caller(blacklisted),
            publicGame, comments[0], readOnlyGame, comments[1], blogId, comments[2],
            publicationId, comments[3], topicId, comments[4]);
    }

    private static Guid AddGame(DmDbContext db, Guid masterId, CommentsAccessMode mode)
    {
        var game = new DbGame
        {
            GameId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = masterId,
            Title = mode == CommentsAccessMode.Public ? "Игра с открытым обсуждением" : "Игра, где пишут свои",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Public,
            CommentsAccessMode = mode,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            ActivatedUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Games.Add(game);
        db.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(game.GameId));
        return game.GameId;
    }

    private static Guid AddComment(DmDbContext db, Guid entityId, Guid authorId, DateTimeOffset now)
    {
        var commentId = Guid.NewGuid();
        db.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = entityId,
            AuthorId = authorId,
            CreatedUtc = now.AddHours(-1),
            Text = "Комментарий, который читают все"
        });
        return commentId;
    }

    private static GeneralUser Caller(DbUser user) =>
        new() { UserId = user.UserId, Username = user.Username, Role = UserRole.RegularUser };

    private static DbUser AddUser(DmDbContext db, string prefix)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@discussionreading.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        db.Users.Add(user);
        return user;
    }
}
