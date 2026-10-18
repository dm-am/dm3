using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Web.API.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// What a removed account wrote stays where it was written, signed by nobody.
/// </summary>
/// <remarks>
/// The account row is hidden by the global soft-delete filter, and the author of a
/// topic, a post or a comment and the master of a game are required navigations.
/// Nested into a projection, a required navigation to a filtered row is an inner
/// join, so the row the author wrote fell out of every list together with the
/// author: the removed master's game vanished from the catalogue, his topic from
/// its board, his post from the room it moved on. After the DM2 import every
/// account removed there over twenty years authors part of every old game, so this
/// is not an edge of the product but most of its history.
///
/// Each surface is asked about a world of its own, written straight to the tables:
/// what is under test is how the reads treat an author that is already gone, not
/// how the author got there. The client renders a missing author as "Аккаунт
/// удален", so a null is the whole of the expected answer - and nothing of the
/// removed row may come with it, neither the name nor the address.
/// </remarks>
public class RemovedAuthorContentShould : IntegrationTestBase
{
    public RemovedAuthorContentShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>Everything the removed account left behind, by identifier.</summary>
    private sealed record World(
        string Marker,
        Guid UserId,
        string Username,
        string Email,
        Guid BoardId,
        Guid TopicId,
        Guid CommentId,
        Guid HostedGameId,
        Guid HostedRoomId,
        Guid HostedPostId,
        Guid GuestGameId,
        Guid GuestRoomId,
        Guid GuestPostId,
        Guid CharacterId);

    /// <summary>
    /// One read: the address, the row the removed account is behind, and the member
    /// that names him.
    /// </summary>
    private sealed record Surface(string Url, Guid RowId, string Member, bool Single = false);

    public static TheoryData<string> Surfaces =>
    [
        "board topics",
        "topics across boards",
        "topic",
        "topic comments",
        "games",
        "game",
        "rated posts",
        "room posts in his game",
        "room posts in another game",
        "characters of another game",
    ];

    private static Surface Read(string surface, World w) => surface switch
    {
        "board topics" => new($"/v1/boards/{w.BoardId}/topics", w.TopicId, "author"),
        "topics across boards" => new($"/v1/topics?search={w.Marker}", w.TopicId, "author"),
        "topic" => new($"/v1/topics/{w.TopicId}", w.TopicId, "author", Single: true),
        "topic comments" => new($"/v1/topics/{w.TopicId}/comments", w.CommentId, "author"),
        "games" => new($"/v1/games?search={w.Marker}", w.HostedGameId, "master"),
        "game" => new($"/v1/games/{w.HostedGameId}", w.HostedGameId, "master", Single: true),
        "rated posts" => new($"/v1/posts?gameId={w.HostedGameId}", w.HostedPostId, "author"),
        "room posts in his game" => new($"/v1/rooms/{w.HostedRoomId}/posts", w.HostedPostId, "author"),
        "room posts in another game" => new($"/v1/rooms/{w.GuestRoomId}/posts", w.GuestPostId, "author"),
        "characters of another game" => new($"/v1/games/{w.GuestGameId}/characters", w.CharacterId, "author"),
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };

    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task KeepTheRowAndDropTheAuthor(string surface)
    {
        var world = await SeedRemovedAuthorAsync();
        var read = Read(surface, world);

        // A guest and a signed-in stranger: the anonymous read is the one a search
        // engine and the DM2 links arrive with, the signed-in one goes through the
        // per-reader enrichment (unread marks, likes) the guest skips.
        foreach (var reader in new[] { null, CustomWebApplicationFactory.CreateSecondUser() })
        {
            var request = reader is null
                ? new HttpRequestMessage(HttpMethod.Get, read.Url)
                : CreateAuthenticatedRequest(HttpMethod.Get, read.Url, reader);
            var response = await Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "{0} is readable by anybody, whoever wrote it; body was: {1}", surface, body);

            using var document = JsonDocument.Parse(body);
            var row = read.Single
                ? document.RootElement.GetProperty("resource")
                : document.RootElement.GetProperty("resources").EnumerateArray()
                    .SingleOrDefault(r => r.GetProperty("id").GetGuid() == read.RowId);

            row.ValueKind.Should().Be(JsonValueKind.Object,
                "the row a removed account wrote stays in {0}; body was: {1}", surface, body);
            row.GetProperty("id").GetGuid().Should().Be(read.RowId);

            var named = row.TryGetProperty(read.Member, out var author) &&
                        author.ValueKind != JsonValueKind.Null;
            named.Should().BeFalse(
                "a removed account is nobody: the client signs a missing {0} as \"Аккаунт удален\"",
                read.Member);

            body.Should().NotContain(world.Username,
                "the removed login must not come out through {0} in any member", surface);
            body.Should().NotContain(world.Email,
                "the removed address must not come out through {0} in any member", surface);
            body.Should().NotContain(world.UserId.ToString(),
                "the removed account's identifier must not come out through {0} either", surface);
        }
    }

    /// <summary>
    /// A removed account with a topic and a comment on its own board, a game it
    /// masters with a rated post in an open room, and a character with a post in a
    /// game somebody else masters.
    /// </summary>
    private async Task<World> SeedRemovedAuthorAsync()
    {
        // The marker names the titles the lists are searched by; the login is a
        // string of its own, so finding it in a body can only mean the account
        // came out and never that a title did.
        var marker = "gone" + Guid.NewGuid().ToString("N")[..10];
        await using var db = DatabaseFixture.CreateDbContext();

        var removedId = Guid.NewGuid();
        var username = "rm" + Guid.NewGuid().ToString("N")[..12];
        var email = $"{username}@removed.example.com";
        db.Users.Add(new DbUser
        {
            UserId = removedId,
            Username = username,
            Email = email,
            PasswordHash = "hash",
            Salt = "salt",
            CreatedUtc = DateTimeOffset.UtcNow.AddYears(-3),
            QualityRating = 5,
            QuantityRating = 2,
        });

        var boardId = Guid.NewGuid();
        db.Set<DbBoard>().Add(new DbBoard
        {
            BoardId = boardId,
            Title = $"Board {marker}",
            Alias = $"b-{marker}",
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.RegularUser,
            TopicsCount = 1,
        });

        var topicId = Guid.NewGuid();
        db.Set<DbTopic>().Add(new DbTopic
        {
            TopicId = topicId,
            BoardId = boardId,
            AuthorId = removedId,
            TopicNumber = 1,
            Title = $"Topic {marker}",
            Text = "What the removed account had to say.",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
        });

        var commentId = Guid.NewGuid();
        db.Set<DbComment>().Add(new DbComment
        {
            CommentId = commentId,
            EntityId = topicId,
            AuthorId = removedId,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Text = "And a reply to it.",
        });

        var hostedGameId = Guid.NewGuid();
        var hostedRoomId = Guid.NewGuid();
        var hostedPostId = Guid.NewGuid();
        AddGame(db, hostedGameId, removedId, $"Game {marker}");
        AddRoom(db, hostedRoomId, hostedGameId);
        db.Set<DbPost>().Add(new DbPost
        {
            PostId = hostedPostId,
            RoomId = hostedRoomId,
            AuthorId = removedId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-5),
            GameText = "The master sets the scene.",
        });
        db.Set<DbPostReview>().Add(new DbPostReview
        {
            PostReviewId = Guid.NewGuid(),
            PostId = hostedPostId,
            PostAuthorId = removedId,
            AuthorId = TestConstants.SecondUserId,
            GameId = hostedGameId,
            SignValue = 1,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-4),
        });

        var guestGameId = Guid.NewGuid();
        var guestRoomId = Guid.NewGuid();
        var guestPostId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        AddGame(db, guestGameId, TestConstants.SecondUserId, $"Other game {marker[..8]}");
        AddRoom(db, guestRoomId, guestGameId);
        db.Set<DbCharacter>().Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = guestGameId,
            AuthorId = removedId,
            Name = "Character of the removed",
            Status = CharacterStatus.Active,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
        });
        db.Set<DbPost>().Add(new DbPost
        {
            PostId = guestPostId,
            RoomId = guestRoomId,
            AuthorId = removedId,
            CharacterId = characterId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-3),
            GameText = "The character answers.",
        });

        await db.SaveChangesAsync();

        // Removed after everything is written, the way it happens: an account is
        // removed with its content already in place.
        var removed = await db.Users.SingleAsync(u => u.UserId == removedId);
        removed.IsRemoved = true;
        await db.SaveChangesAsync();

        return new World(marker, removedId, username, email, boardId, topicId, commentId,
            hostedGameId, hostedRoomId, hostedPostId, guestGameId, guestRoomId, guestPostId, characterId);
    }

    private static void AddGame(Infrastructure.Persistence.DmDbContext db, Guid gameId, Guid masterId, string title)
    {
        db.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = title,
            MasterId = masterId,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-10),
            ActivatedUtc = DateTimeOffset.UtcNow.AddDays(-9),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
        });
        db.Add(GameSheetTestHelper.Sheet(gameId));
    }

    private static void AddRoom(Infrastructure.Persistence.DmDbContext db, Guid roomId, Guid gameId)
    {
        db.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Open room",
            AccessType = RoomAccessType.Open,
            OrderNumber = 1,
        });
    }
}
