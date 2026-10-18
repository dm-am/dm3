using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Web.API.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// "Аккаунт удален" is a profile like any other, made of everything removed accounts
/// left, and of nothing that was theirs alone.
/// </summary>
/// <remarks>
/// The name belongs to no row. The profile adds up the public counters of every removed
/// account, and its tabs are the ordinary lists asked by that name - so the lists keep
/// their own access rules, and a removed account's topic on a closed board stays as
/// closed as it was. Everywhere else the name is a name nobody holds.
///
/// The removed accounts here are made by the test and written straight to the tables:
/// the database is shared by the whole suite and holds removed accounts of other tests,
/// so the sums are asserted as differences, and the lists by rows only this test made.
/// </remarks>
public class RemovedAccountProfileShould : IntegrationTestBase
{
    /// <summary>
    /// The key CommunityProfileService keeps the summary under. The summary is cached for
    /// a minute for every reader alike, so a test that measures what one more removed
    /// account adds to it has to drop the entry on both sides of the change.
    /// </summary>
    private const string SummaryCacheKey = "removed_account_summary";

    /// <summary>The "е" with two dots, spelled as its code point as the sources require.</summary>
    private const char DottedE = (char)0x0451;

    public RemovedAccountProfileShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    public static TheoryData<string> SpellingsOfTheName =>
    [
        RemovedAccount.Name,
        "Аккаунт Удален",
        "АККАУНТ УДАЛЕН",
        $"Аккаунт удал{DottedE}н",
    ];

    [Theory]
    [MemberData(nameof(SpellingsOfTheName))]
    public async Task OpenForAGuestUnderAnySpellingOfTheName(string spelling)
    {
        var response = await Client.GetAsync($"/v1/users/{Uri.EscapeDataString(spelling)}/profile");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a reader reaches the profile the way he types the name; body was: {0}", body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("resource").GetProperty("username").GetString()
            .Should().Be(RemovedAccount.Name, "the profile is written as the site writes the name");
    }

    /// <summary>
    /// A lookalike is refused as an account name, and it does not open the profile
    /// either: the address answers the name, and nobody holds the lookalike.
    /// </summary>
    [Fact]
    public async Task AnswerALookalikeAsAnUnknownName()
    {
        var response = await Client.GetAsync($"/v1/users/{Uri.EscapeDataString("Aккаунт удален")}/profile");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CarryNothingOfAnyRemovedAccount()
    {
        var removed = await SeedRemovedAccountAsync(quality: 3, quantity: 2);
        await DropSummaryAsync();

        foreach (var reader in new[] { null, CustomWebApplicationFactory.CreateSecondUser() })
        {
            var profile = await ProfileAsync(reader);
            var text = profile.GetRawText();

            profile.GetProperty("id").GetGuid().Should().Be(Guid.Empty, "no row is behind the profile");
            profile.GetProperty("usernameHistory").GetArrayLength().Should().Be(0,
                "a name nobody held has no history, and a removed account's history is its own");
            foreach (var member in new[] { "email", "registeredUtc", "lastActivityUtc", "status", "name", "location", "birthday" })
            {
                (profile.TryGetProperty(member, out var value) && value.ValueKind != JsonValueKind.Null)
                    .Should().BeFalse("{0} would be somebody's, and the profile is nobody's", member);
            }

            text.Should().NotContain(removed.Username, "the removed login must not come out of the profile");
            text.Should().NotContain(removed.Email, "the removed address must not come out of the profile");
            text.Should().NotContain(removed.UserId.ToString(), "the removed identifier must not come out either");
        }
    }

    /// <summary>
    /// One more removed account moves every sum by exactly its own public counters, and
    /// a game or a blog counts once however many removed accounts it holds.
    /// </summary>
    [Fact]
    public async Task GrowByExactlyWhatARemovedAccountLeft()
    {
        await DropSummaryAsync();
        var before = await ProfileAsync(reader: null);

        var removed = await SeedRemovedAccountAsync(quality: 7, quantity: 3);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var second = AddUser(db, isRemoved: true);
            // The same game hosted by a second removed account as its assistant: the
            // game is one more, not two.
            db.Set<DM.Infrastructure.Persistence.Entities.Game.Links.GameAssistant>().Add(new()
            {
                GameAssistantId = Guid.NewGuid(),
                GameId = removed.HostedGameId,
                UserId = second.UserId,
                JoinedUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await DropSummaryAsync();
        var after = await ProfileAsync(reader: null);

        int Rating(JsonElement p, string member) => p.GetProperty("rating").GetProperty(member).GetInt32();
        int Count(JsonElement p, string member) =>
            p.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

        (Rating(after, "postReviewScoreSum") - Rating(before, "postReviewScoreSum")).Should().Be(7);
        (Rating(after, "totalPosts") - Rating(before, "totalPosts")).Should().Be(3);
        (Count(after, "postReviewsGiven") - Count(before, "postReviewsGiven")).Should().Be(1);
        (Count(after, "postReviewsReceived") - Count(before, "postReviewsReceived")).Should().Be(1);
        (Count(after, "gamesHosting") - Count(before, "gamesHosting")).Should().Be(2,
            "it masters an active game and a draft, and the removed assistant of the first adds " +
            "no third: a game is one row in the list the tab opens, however many removed hosts it has");
        (Count(after, "gamesPlaying") - Count(before, "gamesPlaying")).Should().Be(1);
        (Count(after, "blogsHosting") - Count(before, "blogsHosting")).Should().Be(1);
    }

    /// <summary>
    /// The tabs of the profile are the ordinary lists asked by the name, and they keep the
    /// rules those lists have for every reader.
    /// </summary>
    [Fact]
    public async Task ListWhatRemovedAccountsLeftAndOnlyWhatTheReaderMaySee()
    {
        var removed = await SeedRemovedAccountAsync(quality: 0, quantity: 0);
        var name = Uri.EscapeDataString(RemovedAccount.Name);

        var topics = await IdsAsync($"/v1/topics?authorUsernames={name}&search={removed.Marker}");
        topics.Should().Contain(removed.OpenTopicId, "the topic stays on its board, signed by nobody")
            .And.NotContain(removed.ClosedTopicId, "a board the guest cannot open stays closed under this name too");

        var hosted = await IdsAsync($"/v1/games?hostUsernames={name}&search={removed.Marker}");
        hosted.Should().Contain(removed.HostedGameId)
            .And.NotContain(removed.DraftGameId, "a private draft is nobody's to see, whoever hosts it");

        var played = await IdsAsync(
            $"/v1/games?playerUsername={name}&playerParticipation=Any&search={removed.Marker}");
        played.Should().Contain(removed.PlayedGameId);

        var posts = await IdsAsync($"/v1/posts?authorUsernames={name}&gameId={removed.HostedGameId}");
        posts.Should().Contain(removed.RatedPostId, "the best post of the profile comes from the rated feed");

        var closedRoom = await Client.GetAsync($"/v1/rooms/{removed.ClosedRoomId}/posts?authorUsernames={name}");
        closedRoom.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a room the guest cannot enter is not there for him under any name");

        var blogs = await IdsAsync($"/v1/blogs?hostUsernames={name}");
        blogs.Should().Contain(removed.BlogId);

        var byOldLogin = await IdsAsync(
            $"/v1/topics?authorUsernames={Uri.EscapeDataString(removed.Username)}&search={removed.Marker}");
        byOldLogin.Should().BeEmpty("the removed login is nobody's now, and must not find what the removal took off it");
    }

    /// <summary>
    /// Asked together with a live name, the list answers both: the name narrows by the
    /// removed accounts and the other name by its own account.
    /// </summary>
    [Fact]
    public async Task ListTheNameAlongsideALiveAuthor()
    {
        var removed = await SeedRemovedAccountAsync(quality: 0, quantity: 0);
        Guid liveTopicId;
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            liveTopicId = AddTopic(db, removed.OpenBoardId, TestConstants.SecondUserId, 3, $"Live {removed.Marker}");
            await db.SaveChangesAsync();
        }

        var topics = await IdsAsync(
            $"/v1/topics?authorUsernames={Uri.EscapeDataString(RemovedAccount.Name)}" +
            $"&authorUsernames={Uri.EscapeDataString(TestConstants.SecondUserUsername)}&search={removed.Marker}");

        topics.Should().Contain([removed.OpenTopicId, liveTopicId]);
    }

    public static TheoryData<string> OtherReadsByName =>
    [
        "/v1/users/{0}",
        "/v1/users/{0}/best-topic",
        "/v1/users/{0}/endorsements",
        "/v1/users/{0}/written-endorsements",
        "/v1/users/{0}/game-reviews",
        "/v1/users/{0}/achievements",
        "/v1/users/{0}/awards",
        "/v1/users/{0}/subscribers",
    ];

    /// <summary>
    /// Every other read that takes a name answers this one as it answers a name nobody
    /// holds: the profile is the only thing the name opens.
    /// </summary>
    [Theory]
    [MemberData(nameof(OtherReadsByName))]
    public async Task AnswerEveryOtherReadAsAnUnknownName(string template)
    {
        var unknown = "nobody" + Guid.NewGuid().ToString("N")[..8];

        var asReserved = await Client.GetAsync(string.Format(template, Uri.EscapeDataString(RemovedAccount.Name)));
        var asUnknown = await Client.GetAsync(string.Format(template, unknown));

        asReserved.StatusCode.Should().Be(asUnknown.StatusCode,
            "{0} must not tell the name apart from a name nobody holds", template);
        asReserved.IsSuccessStatusCode.Should().BeFalse(
            "a name nobody holds has nothing to show at {0}", template);
    }

    public static TheoryData<string> ReservedSpellings =>
    [
        RemovedAccount.Name,
        "Аккаунт Удален",
        $"Аккаунт удал{DottedE}н",
        "Aккаунт удален",
        "Аккаунт_удален",
    ];

    [Theory]
    [MemberData(nameof(ReservedSpellings))]
    public async Task RefuseTheNameToAnyAccount(string spelling)
    {
        var response = await Client.PostAsJsonAsync("/v1/account/check-username", new { username = spelling });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"isAvailable\":false",
            "an account under {0} would sign its posts as everything removed accounts wrote", spelling);
    }

    [Fact]
    public async Task RefuseTheNameAtActivation()
    {
        var act = () => UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, "Aккаунт удален", "TestPass123ok",
            $"reserved{Guid.NewGuid():N}@test.example.com");

        await act.Should().ThrowAsync<InvalidOperationException>(
            "the activation that picks the login refuses the reserved name as a taken one");
    }

    /// <summary>
    /// A removed row that carries the name stays removed: brought back it would be a live
    /// account signing its posts as all the removed ones. The DM2 shared account arrives
    /// exactly like this.
    /// </summary>
    [Fact]
    public async Task RefuseToRestoreARowUnderTheName()
    {
        // Mixed case, not the spelling the other tests use: the unique index over the
        // name is case-insensitive, and another test of this run may hold the plain one.
        var storedName = "аККаунт уДАЛЕН";
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var existing = await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Username.ToLower() == storedName.ToLower());
            if (!existing)
            {
                db.Users.Add(new DbUser
                {
                    UserId = Guid.NewGuid(),
                    Username = storedName,
                    Email = $"shared{Guid.NewGuid():N}@dm2.example.com",
                    PasswordHash = "hash",
                    Salt = "salt",
                    IsRemoved = true,
                });
                await db.SaveChangesAsync();
            }
        }

        var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Post,
            $"/v1/moderation/users/{Uri.EscapeDataString(storedName)}/account",
            CustomWebApplicationFactory.CreateSeniorModeratorUser()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "body was: {0}", await response.Content.ReadAsStringAsync());
        await using var check = DatabaseFixture.CreateDbContext();
        (await check.Users.IgnoreQueryFilters().SingleAsync(u => u.Username == storedName)).IsRemoved
            .Should().BeTrue("the row stays removed");
    }

    // ═══ World ═══

    private sealed record Removed(
        string Marker,
        Guid UserId,
        string Username,
        string Email,
        Guid OpenBoardId,
        Guid OpenTopicId,
        Guid ClosedTopicId,
        Guid HostedGameId,
        Guid DraftGameId,
        Guid PlayedGameId,
        Guid RatedPostId,
        Guid ClosedRoomId,
        Guid BlogId);

    /// <summary>
    /// A removed account with a topic on an open board and one on a closed board, an
    /// active game it masters with a rated post in an open room and a post in a closed
    /// one, a private draft it masters, a character in somebody else's game, a blog, and
    /// one post review given. Removed after everything is written, as it happens.
    /// </summary>
    private async Task<Removed> SeedRemovedAccountAsync(int quality, int quantity)
    {
        var marker = "rmp" + Guid.NewGuid().ToString("N")[..10];
        await using var db = DatabaseFixture.CreateDbContext();

        var user = AddUser(db, isRemoved: false, quality, quantity);

        var openBoardId = AddBoard(db, marker + "o", BoardAccessPolicy.Guest | BoardAccessPolicy.RegularUser |
            BoardAccessPolicy.Mentor | BoardAccessPolicy.Moderator | BoardAccessPolicy.SeniorModerator |
            BoardAccessPolicy.Administrator);
        var closedBoardId = AddBoard(db, marker + "c", BoardAccessPolicy.Administrator);
        var openTopicId = AddTopic(db, openBoardId, user.UserId, 1, $"Open {marker}");
        var closedTopicId = AddTopic(db, closedBoardId, user.UserId, 1, $"Closed {marker}");

        var hostedGameId = AddGame(db, user.UserId, $"Hosted {marker}", ModuleStatus.Active);
        var draftGameId = AddGame(db, user.UserId, $"Draft {marker}", ModuleStatus.Draft);
        var playedGameId = AddGame(db, TestConstants.SecondUserId, $"Played {marker}", ModuleStatus.Active);

        var openRoomId = AddRoom(db, hostedGameId, 1, RoomAccessType.Open);
        var closedRoomId = AddRoom(db, hostedGameId, 2, RoomAccessType.Private);
        var ratedPostId = AddPost(db, openRoomId, user.UserId);
        AddPost(db, closedRoomId, user.UserId);
        db.Set<DbPostReview>().Add(new DbPostReview
        {
            PostReviewId = Guid.NewGuid(),
            PostId = ratedPostId,
            PostAuthorId = user.UserId,
            AuthorId = TestConstants.SecondUserId,
            GameId = hostedGameId,
            SignValue = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        db.Set<DbCharacter>().Add(new DbCharacter
        {
            CharacterId = Guid.NewGuid(),
            GameId = playedGameId,
            AuthorId = user.UserId,
            Name = "Character " + marker,
            Status = CharacterStatus.Active,
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        // The review it gave, on a post of somebody else's.
        var playedRoomId = AddRoom(db, playedGameId, 1, RoomAccessType.Open);
        var othersPostId = AddPost(db, playedRoomId, TestConstants.SecondUserId);
        db.Set<DbPostReview>().Add(new DbPostReview
        {
            PostReviewId = Guid.NewGuid(),
            PostId = othersPostId,
            PostAuthorId = TestConstants.SecondUserId,
            AuthorId = user.UserId,
            GameId = playedGameId,
            SignValue = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        var blogId = Guid.NewGuid();
        db.Blogs.Add(new DbBlog
        {
            BlogId = blogId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = user.UserId,
            Title = $"Blog {marker}",
            Description = "A blog of an account that left",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });

        await db.SaveChangesAsync();

        var row = await db.Users.SingleAsync(u => u.UserId == user.UserId);
        row.IsRemoved = true;
        await db.SaveChangesAsync();

        return new Removed(marker, user.UserId, user.Username, user.Email, openBoardId, openTopicId, closedTopicId,
            hostedGameId, draftGameId, playedGameId, ratedPostId, closedRoomId, blogId);
    }

    private static DbUser AddUser(
        Infrastructure.Persistence.DmDbContext db, bool isRemoved, int quality = 0, int quantity = 0)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            Username = $"gone{userId:N}"[..20],
            Email = $"{userId:N}@removed.example.com",
            PasswordHash = "hash",
            Salt = "salt",
            CreatedUtc = DateTimeOffset.UtcNow.AddYears(-2),
            QualityRating = quality,
            QuantityRating = quantity,
            IsRemoved = isRemoved,
        };
        db.Users.Add(user);
        return user;
    }

    private static Guid AddBoard(Infrastructure.Persistence.DmDbContext db, string alias, BoardAccessPolicy view)
    {
        var boardId = Guid.NewGuid();
        db.Set<DbBoard>().Add(new DbBoard
        {
            BoardId = boardId,
            Title = "Board " + alias,
            Alias = alias,
            Order = 0,
            ViewPolicy = view,
            CreateTopicPolicy = BoardAccessPolicy.RegularUser,
        });
        return boardId;
    }

    private static Guid AddTopic(Infrastructure.Persistence.DmDbContext db, Guid boardId, Guid authorId, int number, string title)
    {
        var topicId = Guid.NewGuid();
        db.Set<DbTopic>().Add(new DbTopic
        {
            TopicId = topicId,
            BoardId = boardId,
            AuthorId = authorId,
            TopicNumber = number,
            Title = title,
            Text = "Text",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
        });
        return topicId;
    }

    private static Guid AddGame(Infrastructure.Persistence.DmDbContext db, Guid masterId, string title, ModuleStatus status)
    {
        var gameId = Guid.NewGuid();
        db.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = title,
            MasterId = masterId,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-10),
            ActivatedUtc = status == ModuleStatus.Active ? DateTimeOffset.UtcNow.AddDays(-9) : null,
            Status = status,
            DraftVisibility = DraftVisibility.Private,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
        });
        db.Add(GameSheetTestHelper.Sheet(gameId));
        return gameId;
    }

    private static Guid AddRoom(Infrastructure.Persistence.DmDbContext db, Guid gameId, int number, RoomAccessType access)
    {
        var roomId = Guid.NewGuid();
        db.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = number,
            Title = "Room " + number,
            AccessType = access,
            OrderNumber = number,
        });
        return roomId;
    }

    private static Guid AddPost(Infrastructure.Persistence.DmDbContext db, Guid roomId, Guid authorId)
    {
        var postId = Guid.NewGuid();
        db.Set<DbPost>().Add(new DbPost
        {
            PostId = postId,
            RoomId = roomId,
            AuthorId = authorId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-2),
            GameText = "A post",
        });
        return postId;
    }

    // ═══ Reads ═══

    private async Task DropSummaryAsync()
    {
        var cache = DatabaseFixture.Factory.Services.GetRequiredService<ICache>();
        await cache.InvalidateAsync(SummaryCacheKey);
    }

    private async Task<JsonElement> ProfileAsync(Domain.Core.Dto.GeneralUser? reader)
    {
        var url = $"/v1/users/{Uri.EscapeDataString(RemovedAccount.Name)}/profile";
        var response = reader is null
            ? await Client.GetAsync(url)
            : await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, reader));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("resource").Clone();
    }

    /// <summary>The identifiers of one page of a list, read as a guest.</summary>
    private async Task<Guid[]> IdsAsync(string url)
    {
        var response = await Client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "GET {0} answered {1}", url, body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("resources").EnumerateArray()
            .Select(row => row.GetProperty("id").GetGuid())
            .ToArray();
    }
}
