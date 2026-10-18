using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DM.Web.API.IntegrationTests.Helpers;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbLike = DM.Infrastructure.Persistence.Entities.Shared.Like;
using DbNotepadEntry = DM.Infrastructure.Persistence.Entities.Personal.Notepads.NotepadEntry;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A game or a blog the reader may not see is not there to him, with everything
/// in it, end to end: every address that reaches into it answers exactly as the
/// same address over identifiers that are nothing, and every list that could name
/// what is in it leaves it out.
/// </summary>
/// <remarks>
/// A module the user may not see is unseen whole: what it holds would name it,
/// and a refusal worded apart from absence says it exists. Two ways hide a module
/// from a stranger and a guest - a private draft and premoderation - and each case
/// runs under both.
///
/// Asserted as indistinguishability, body included, as the visibility suites next
/// door are: a 404 in other words is still an answer to whether the id is real.
/// Beside every hidden module stands a twin that is open, holding the same things,
/// so a route that answers the same on everything is not mistaken for a hiding
/// one: on the open twin the stranger has to get something else.
/// </remarks>
public class HiddenModuleShould : IntegrationTestBase
{
    public HiddenModuleShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>What hides the module from somebody holding no role in it.</summary>
    public enum Hiding
    {
        PrivateDraft,
        AwaitingEdits
    }

    public static TheoryData<Hiding> Hidings => new() { Hiding.PrivateDraft, Hiding.AwaitingEdits };

    /// <summary>Everything a game holds that has an address of its own.</summary>
    private sealed record GameContent(
        Guid Game,
        Guid Character,
        Guid Room,
        Guid ChatRoom,
        Guid Post,
        Guid PostReview,
        Guid GameReview,
        Guid Comment,
        Guid NotepadEntry)
    {
        public static GameContent Nothing() => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    /// <summary>Everything a blog holds that has an address of its own.</summary>
    private sealed record BlogContent(
        Guid Blog,
        Guid Publication,
        Guid BlogComment,
        Guid PublicationComment,
        Guid NotepadEntry)
    {
        public static BlogContent Nothing() => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    /// <summary>One address into a module, and whether the open twin serves it to anybody.</summary>
    private sealed record Route<T>(string Name, HttpMethod Method, Func<T, string> Url, bool OpenToAll = false);

    private static readonly Route<GameContent>[] GameRoutes =
    [
        new("the game", HttpMethod.Get, c => $"/v1/games/{c.Game}", true),
        new("the game's details", HttpMethod.Get, c => $"/v1/games/{c.Game}/details", true),
        new("the characters", HttpMethod.Get, c => $"/v1/games/{c.Game}/characters", true),
        new("a character", HttpMethod.Get, c => $"/v1/characters/{c.Character}", true),
        new("a character's notepad", HttpMethod.Get, c => $"/v1/characters/{c.Character}/notepad"),
        new("a character's master notepad", HttpMethod.Get, c => $"/v1/characters/{c.Character}/master-notepad"),
        new("the rooms", HttpMethod.Get, c => $"/v1/games/{c.Game}/rooms", true),
        new("a room", HttpMethod.Get, c => $"/v1/rooms/{c.Room}", true),
        new("the posts of a room", HttpMethod.Get, c => $"/v1/rooms/{c.Room}/posts", true),
        new("the first unread post of a room", HttpMethod.Get, c => $"/v1/rooms/{c.Room}/posts/first-unread", true),
        new("a post", HttpMethod.Get, c => $"/v1/posts/{c.Post}", true),
        new("the ratings of a post", HttpMethod.Get, c => $"/v1/posts/{c.Post}/reviews", true),
        new("a rating of a post", HttpMethod.Get, c => $"/v1/posts/{c.Post}/reviews/{c.PostReview}", true),
        new("the chat rooms", HttpMethod.Get, c => $"/v1/games/{c.Game}/chat-rooms", true),
        new("a chat room", HttpMethod.Get, c => $"/v1/chat-rooms/{c.ChatRoom}", true),
        new("the messages of a chat room", HttpMethod.Get, c => $"/v1/chat-rooms/{c.ChatRoom}/messages", true),
        new("the reviews", HttpMethod.Get, c => $"/v1/games/{c.Game}/reviews", true),
        new("a review", HttpMethod.Get, c => $"/v1/games/{c.Game}/reviews/{c.GameReview}", true),
        new("the discussion", HttpMethod.Get, c => $"/v1/games/{c.Game}/comments", true),
        new("a comment", HttpMethod.Get, c => $"/v1/games/comments/{c.Comment}", true),
        new("the first unread comment", HttpMethod.Get, c => $"/v1/games/{c.Game}/comments/first-unread", true),
        new("the first unread post", HttpMethod.Get, c => $"/v1/games/{c.Game}/posts/first-unread", true),
        new("the notepad", HttpMethod.Get, c => $"/v1/games/{c.Game}/notepad"),
        new("a notepad entry", HttpMethod.Get, c => $"/v1/games/{c.Game}/notepad/{c.NotepadEntry}"),
        new("the people", HttpMethod.Get, c => $"/v1/games/{c.Game}/users", true),
        new("the assistants", HttpMethod.Get, c => $"/v1/games/{c.Game}/users/assistants", true),
        new("the readers", HttpMethod.Get, c => $"/v1/games/{c.Game}/readers", true),
        new("the blacklist", HttpMethod.Get, c => $"/v1/games/{c.Game}/blacklist"),
        new("the invitations", HttpMethod.Get, c => $"/v1/games/{c.Game}/invitations"),
        new("marking the discussion read", HttpMethod.Delete, c => $"/v1/games/{c.Game}/comments/unread"),
        new("marking the characters read", HttpMethod.Delete, c => $"/v1/games/{c.Game}/characters/unread"),
        new("marking a room read", HttpMethod.Delete, c => $"/v1/rooms/{c.Room}/posts/unread"),
        new("marking a chat room read", HttpMethod.Delete, c => $"/v1/chat-rooms/{c.ChatRoom}/messages/unread"),
        new("commenting", HttpMethod.Post, c => $"/v1/games/{c.Game}/comments"),
    ];

    private static readonly Route<BlogContent>[] BlogRoutes =
    [
        new("the blog", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}", true),
        new("the publications", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/publications", true),
        new("a publication", HttpMethod.Get, c => $"/v1/publications/{c.Publication}", true),
        new("the comments of a publication", HttpMethod.Get, c => $"/v1/publications/{c.Publication}/comments", true),
        new("a comment of a publication", HttpMethod.Get, c => $"/v1/publications/comments/{c.PublicationComment}", true),
        new("the discussion", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/comments", true),
        new("a comment", HttpMethod.Get, c => $"/v1/blogs/comments/{c.BlogComment}", true),
        new("the people", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/users", true),
        new("the assistants", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/users/assistants", true),
        new("the readers", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/readers", true),
        new("the notepad", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/notepad"),
        new("a notepad entry", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/notepad/{c.NotepadEntry}"),
        new("the blacklist", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/blacklist"),
        new("the invitations", HttpMethod.Get, c => $"/v1/blogs/{c.Blog}/invitations"),
        new("marking the discussion read", HttpMethod.Delete, c => $"/v1/blogs/{c.Blog}/comments/unread"),
        new("commenting", HttpMethod.Post, c => $"/v1/blogs/{c.Blog}/comments"),
        new("subscribing", HttpMethod.Post, c => $"/v1/blogs/{c.Blog}/readers"),
        new("unsubscribing", HttpMethod.Delete, c => $"/v1/blogs/{c.Blog}/readers"),
    ];

    [Theory]
    [MemberData(nameof(Hidings))]
    public async Task AnswerEveryAddressIntoAHiddenGameAsIntoNothing(Hiding hiding)
    {
        var world = await SeedGamesAsync(hiding);
        await SweepAsync(GameRoutes, world.Hidden, world.Open, GameContent.Nothing, world.Stranger);
    }

    [Theory]
    [MemberData(nameof(Hidings))]
    public async Task AnswerEveryAddressIntoAHiddenBlogAsIntoNothing(Hiding hiding)
    {
        var world = await SeedBlogsAsync(hiding);
        await SweepAsync(BlogRoutes, world.Hidden, world.Open, BlogContent.Nothing, world.Stranger);
    }

    /// <summary>
    /// The lists that name what a game holds: the reviews on the profiles of its
    /// master and of their author, the posts, the ratings of a post, the search of
    /// messages. The open twin is on each of them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hidings))]
    public async Task LeaveAHiddenGameOutOfEveryListThatNamesWhatItHolds(Hiding hiding)
    {
        var world = await SeedGamesAsync(hiding);

        foreach (var caller in new[] { null, world.Stranger })
        {
            var received = await IdsAsync($"/v1/users/{world.Master.Username}/game-reviews?take=100", caller);
            received.Should().Contain(world.Open.GameReview).And.NotContain(world.Hidden.GameReview,
                "the reviews the master received name the game they were written about");

            var written = await IdsAsync($"/v1/users/{world.Player.Username}/written-game-reviews?take=100", caller);
            written.Should().Contain(world.Open.GameReview).And.NotContain(world.Hidden.GameReview,
                "the reviews the player wrote name the game too");

            var pulse = await IdsAsync($"/v1/posts?authorUsernames={world.Player.Username}&take=100", caller);
            pulse.Should().Contain(world.Open.Post).And.NotContain(world.Hidden.Post);
        }

        var found = await IdsAsync($"/v1/search/messages?search={world.Marker}", world.Stranger);
        found.Should().Contain(world.Open.Post).And.NotContain(world.Hidden.Post,
            "the search finds a post the stranger may read and no other");
    }

    /// <summary>
    /// A closed room is named to whoever sees the game with the characters who
    /// write in it, as the game menu writes it. Of a hidden game the list of
    /// rooms answers as of nothing, those names with it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hidings))]
    public async Task NameTheWritersOfAClosedRoomOnlyInAGameTheReaderSees(Hiding hiding)
    {
        var world = await SeedGamesAsync(hiding);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            foreach (var game in new[] { world.Hidden, world.Open })
            {
                var roomId = Guid.NewGuid();
                db.Set<DbRoom>().Add(new DbRoom
                {
                    RoomId = roomId,
                    GameId = game.Game,
                    RoomNumber = 3,
                    Title = "Закрытая комната",
                    AccessType = RoomAccessType.Private,
                    Type = RoomType.Default,
                    OrderNumber = 3.0
                });
                db.Set<DbRoomAccess>().Add(new DbRoomAccess
                {
                    AccessId = Guid.NewGuid(),
                    RoomId = roomId,
                    CharacterId = game.Character,
                    Policy = RoomAccessPolicy.Full
                });
            }

            await db.SaveChangesAsync();
        }

        var writer = "Персонаж " + world.Open.Character.ToString("N")[..6];
        foreach (var caller in new[] { null, world.Stranger })
        {
            var who = caller == null ? "a guest" : "a stranger";
            var url = $"/v1/games/{world.Open.Game}/rooms";
            var response = await Client.SendAsync(caller == null
                ? new HttpRequestMessage(HttpMethod.Get, url)
                : CreateAuthenticatedRequest(HttpMethod.Get, url, caller));
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            using (var document = JsonDocument.Parse(body))
            {
                var closed = document.RootElement.GetProperty("resources").EnumerateArray()
                    .Single(r => r.GetProperty("title").GetString() == "Закрытая комната");
                closed.GetProperty("canView").GetBoolean().Should().BeFalse();
                closed.GetProperty("characterNames").EnumerateArray().Select(n => n.GetString())
                    .Should().Equal([writer], $"{who} sees the open game and who writes in its closed room");
            }

            (await Send(HttpMethod.Get, $"/v1/games/{world.Hidden.Game}/rooms", caller))
                .Should().Be(await Send(HttpMethod.Get, $"/v1/games/{GameContent.Nothing().Game}/rooms", caller),
                    $"the rooms of a hidden game answer {who} as of nothing");
        }
    }

    /// <summary>
    /// The boards of a past month name the games and the blogs that wrote the most
    /// and were rated the most. A hidden one is not on them, under any heading,
    /// even when it outwrote the open one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hidings))]
    public async Task LeaveHiddenModulesOffTheLeaderboards(Hiding hiding)
    {
        var games = await SeedGamesAsync(hiding);
        var blogs = await SeedBlogsAsync(hiding);
        // A month nothing else in the shared database is dated in. The boards of a
        // past month are cached, so each case draws from years of its own: the
        // other case cannot have asked for the same month before it.
        var period = new DateTimeOffset(1950 + 25 * (int)hiding + Random.Shared.Next(25), 1 + Random.Shared.Next(12),
            15, 12, 0, 0, TimeSpan.Zero);
        await DateIntoPeriodAsync(games, blogs, period);

        var response = await Client.GetAsync($"/v1/leaderboards/{period.Year}/{period.Month}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var boards = document.RootElement.GetProperty("resource");

        Entities(boards, "topGamesByPosts").Should().Contain(games.Open.Game).And.NotContain(games.Hidden.Game);
        Entities(boards, "topGamesByRating").Should().Contain(games.Open.Game).And.NotContain(games.Hidden.Game);
        Entities(boards, "topBlogsByPosts").Should().Contain(blogs.Open.Blog).And.NotContain(blogs.Hidden.Blog);
        Entities(boards, "topBlogsByRating").Should().Contain(blogs.Open.Blog).And.NotContain(blogs.Hidden.Blog);
    }

    /// <summary>
    /// The best post of the week is a post a guest can read: the hidden one is
    /// rated higher and still is not named.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hidings))]
    public async Task NameAsTheBestPostOfTheWeekOnlyAPostAGuestCanRead(Hiding hiding)
    {
        var world = await SeedGamesAsync(hiding);
        var ratings = new List<Guid>();
        try
        {
            ratings.AddRange(await RateAsync(world.Hidden, world.Player.UserId, 30));
            ratings.AddRange(await RateAsync(world.Open, world.Player.UserId, 29));
            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<ICache>().InvalidateAsync("LiveStats");
            }

            var response = await Client.GetAsync("/v1/stats");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            document.RootElement.GetProperty("resource").GetProperty("weeklyBestPost")
                .GetProperty("postId").GetGuid().Should().Be(world.Open.Post);
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.PostReviews.IgnoreQueryFilters().Where(r => ratings.Contains(r.PostReviewId)).ExecuteDeleteAsync();
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ICache>().InvalidateAsync("LiveStats");
        }
    }

    /// <summary>
    /// An unpublished publication is its author's draft, in a blog everybody
    /// reads: the feed shows it to its author and to nobody else.
    /// </summary>
    [Fact]
    public async Task ShowAnUnpublishedPublicationInTheFeedToItsAuthorAlone()
    {
        var world = await SeedBlogsAsync(Hiding.PrivateDraft);
        var draftId = Guid.NewGuid();
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Publications.Add(Publication(draftId, world.Open.Blog, world.Owner.UserId, 2, published: false,
                DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var url = $"/v1/blogs/{world.Open.Blog}/publications";
        foreach (var caller in new[] { null, world.Stranger })
        {
            (await IdsAsync(url, caller)).Should().Contain(world.Open.Publication).And.NotContain(draftId,
                "a draft of a publication is not published to its readers");
        }

        (await IdsAsync(url, world.Owner)).Should().Contain(draftId, "its author reads it in the feed");
    }

    /// <summary>
    /// The draft visibility closes a blog while it is a draft and nothing after: a
    /// blog started from a private draft is read and commented by anybody, as the
    /// lists already show it to anybody.
    /// </summary>
    [Fact]
    public async Task OpenABlogStartedFromAPrivateDraftToEverybody()
    {
        var world = await SeedBlogsAsync(Hiding.PrivateDraft);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            await db.Blogs.Where(b => b.BlogId == world.Hidden.Blog).ExecuteUpdateAsync(b => b
                .SetProperty(x => x.Status, ModuleStatus.Active)
                .SetProperty(x => x.ActivatedUtc, DateTimeOffset.UtcNow));
        }

        foreach (var caller in new[] { null, world.Stranger })
        {
            foreach (var url in new[]
                     {
                         $"/v1/blogs/{world.Hidden.Blog}", $"/v1/blogs/{world.Hidden.Blog}/comments",
                         $"/v1/blogs/{world.Hidden.Blog}/publications", $"/v1/publications/{world.Hidden.Publication}"
                     })
            {
                (await Send(HttpMethod.Get, url, caller)).Status.Should().Be(HttpStatusCode.OK, url);
            }
        }

        (await Send(HttpMethod.Post, $"/v1/blogs/{world.Hidden.Blog}/comments", world.Stranger)).Status
            .Should().Be(HttpStatusCode.Created, "the blog takes comments from anybody who reads it");
    }

    // ═══ THE SWEEP ═══

    private async Task SweepAsync<T>(
        IEnumerable<Route<T>> routes, T hidden, T open, Func<T> nothing, GeneralUser stranger)
    {
        // Every route is answered before the case fails, so one run names every
        // address that tells a hidden module apart, not the first of them.
        using var scope = new AssertionScope();
        foreach (var route in routes)
        {
            foreach (var caller in new[] { null, stranger })
            {
                var who = caller == null ? "a guest" : "a stranger";
                var onNothing = await Send(route.Method, route.Url(nothing()), caller);
                var onHidden = await Send(route.Method, route.Url(hidden), caller);

                onHidden.Should().Be(onNothing, $"{route.Name} of a hidden module answers {who} as of nothing");

                if (route.OpenToAll)
                {
                    (await Send(route.Method, route.Url(open), caller)).Status.Should().Be(HttpStatusCode.OK,
                        $"{route.Name} of the open twin is read by {who}");
                }
                else if (caller != null)
                {
                    (await Send(route.Method, route.Url(open), caller)).Should().NotBe(onNothing,
                        $"{route.Name} of the open twin tells {who} it is there");
                }
            }
        }
    }

    /// <summary>
    /// Status and body together: the body is what tells two 404s apart, and two
    /// lists that both answer 200, one of them with what the hidden module holds.
    /// </summary>
    private sealed record Answer(HttpStatusCode Status, string Body);

    private async Task<Answer> Send(HttpMethod method, string url, GeneralUser? caller)
    {
        var request = caller == null
            ? new HttpRequestMessage(method, url)
            : CreateAuthenticatedRequest(method, url, caller);
        if (method == HttpMethod.Post)
        {
            request.Content = JsonContent.Create(new { text = "Комментарий в чужом модуле" });
        }

        var response = await Client.SendAsync(request);
        return new Answer(response.StatusCode, Normalized(await response.Content.ReadAsStringAsync()));
    }

    /// <summary>
    /// The answer minus its correlation token, which is per request by design,
    /// and with the identifiers written as a placeholder: a sentence that repeats
    /// the id it was given tells the caller nothing new.
    /// </summary>
    private static string Normalized(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return body;

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return body;

        var fields = string.Join("&", document.RootElement
            .EnumerateObject()
            .Where(p => p.Name != "traceId")
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={p.Value.GetRawText()}"));
        return Regex.Replace(fields,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "{id}");
    }

    private async Task<Guid[]> IdsAsync(string url, GeneralUser? caller)
    {
        var request = caller == null
            ? new HttpRequestMessage(HttpMethod.Get, url)
            : CreateAuthenticatedRequest(HttpMethod.Get, url, caller);
        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("resources").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
    }

    private static Guid[] Entities(JsonElement boards, string board) =>
        boards.GetProperty(board).EnumerateArray().Select(e => e.GetProperty("entityId").GetGuid()).ToArray();

    // ═══ SEEDING ═══

    private sealed record GameWorld(
        GeneralUser Master,
        GeneralUser Player,
        GeneralUser Stranger,
        GameContent Hidden,
        GameContent Open,
        string Marker);

    private sealed record BlogWorld(GeneralUser Owner, GeneralUser Stranger, BlogContent Hidden, BlogContent Open);

    /// <summary>
    /// Two games of one master, one hidden the way the case says and one open,
    /// each with a character of the same player, an open room and a chat room, a
    /// post of the character in the room rated by the master, a review of the
    /// player, a comment of the player and an entry of the master's notepad. Fresh
    /// identifiers every time: the fixture database is shared and its usernames
    /// and public ids are uniquely indexed.
    /// </summary>
    private async Task<GameWorld> SeedGamesAsync(Hiding hiding)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var master = AddUser(db, "hmm");
        var player = AddUser(db, "hmp");
        var stranger = AddUser(db, "hms");
        var marker = new string(Enumerable.Range(0, 12).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());

        var hidden = AddGame(db, master.UserId, player.UserId, marker, hiding);
        var open = AddGame(db, master.UserId, player.UserId, marker, hiding: null);
        await db.SaveChangesAsync();

        return new GameWorld(Caller(master), Caller(player), Caller(stranger), hidden, open, marker);
    }

    private static GameContent AddGame(DmDbContext db, Guid masterId, Guid playerId, string marker, Hiding? hiding)
    {
        var now = DateTimeOffset.UtcNow;
        var content = GameContent.Nothing();
        db.Games.Add(new DbGame
        {
            GameId = content.Game,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = masterId,
            Title = hiding == null ? "Открытая игра" : "Скрытая игра",
            Status = hiding == Hiding.PrivateDraft ? ModuleStatus.Draft : ModuleStatus.Active,
            PremoderationStatus = hiding == Hiding.AwaitingEdits
                ? PremoderationStatus.AwaitingEdits
                : PremoderationStatus.Approved,
            DraftVisibility = hiding == Hiding.PrivateDraft ? DraftVisibility.Private : DraftVisibility.Public,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = now.AddDays(-3),
            ActivatedUtc = hiding == Hiding.PrivateDraft ? null : now.AddDays(-2)
        });
        db.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(content.Game));
        db.Set<DbCharacter>().Add(new DbCharacter
        {
            CharacterId = content.Character,
            GameId = content.Game,
            AuthorId = playerId,
            Name = "Персонаж " + content.Character.ToString("N")[..6],
            Status = CharacterStatus.Active,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = now.AddDays(-2)
        });
        var chatId = Guid.NewGuid();
        db.Set<DbRoom>().AddRange(
            new DbRoom
            {
                RoomId = content.Room,
                GameId = content.Game,
                RoomNumber = 1,
                Title = "Открытая комната",
                AccessType = RoomAccessType.Open,
                Type = RoomType.Default,
                OrderNumber = 1.0
            },
            new DbRoom
            {
                RoomId = content.ChatRoom,
                GameId = content.Game,
                RoomNumber = 2,
                Title = "Чат-комната",
                AccessType = RoomAccessType.Open,
                Type = RoomType.Chat,
                OrderNumber = 2.0,
                ChatId = chatId
            });
        db.Chats.Add(new DbChat
        {
            ChatId = chatId,
            Type = ChatType.GameRoom,
            Title = "Чат-комната",
            RoomId = content.ChatRoom,
            PublicId = $"h{chatId:N}"[..10]
        });
        db.Set<DbPost>().Add(new DbPost
        {
            PostId = content.Post,
            RoomId = content.Room,
            CharacterId = content.Character,
            AuthorId = playerId,
            CreatedUtc = now.AddHours(-2),
            GameText = $"Пост со словом {marker}",
            PrivateAddresseeSnapshotJson = "{}"
        });
        db.PostReviews.Add(new DbPostReview
        {
            PostReviewId = content.PostReview,
            AuthorId = masterId,
            PostId = content.Post,
            PostAuthorId = playerId,
            GameId = content.Game,
            CreatedUtc = now.AddHours(-1),
            SignValue = 1
        });
        db.GameReviews.Add(new DbGameReview
        {
            GameReviewId = content.GameReview,
            AuthorId = playerId,
            GameId = content.Game,
            CreatedUtc = now.AddHours(-1),
            Text = "Рецензия игрока"
        });
        db.Comments.Add(new DbComment
        {
            CommentId = content.Comment,
            EntityId = content.Game,
            AuthorId = playerId,
            CreatedUtc = now.AddHours(-1),
            Text = "Комментарий игрока"
        });
        db.Set<DbNotepadEntry>().Add(new DbNotepadEntry
        {
            EntryId = content.NotepadEntry,
            NotepadType = NotepadType.Master,
            ContainerId = content.Game,
            AuthorId = masterId,
            Title = "Заметка мастера",
            Content = "Текст заметки",
            CreatedUtc = now.AddHours(-1)
        });
        return content;
    }

    /// <summary>
    /// Two blogs of one owner, one hidden the way the case says and one open, each
    /// with a published publication, a comment in the discussion and one under the
    /// publication, and an entry of the blog's notepad.
    /// </summary>
    private async Task<BlogWorld> SeedBlogsAsync(Hiding hiding)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var owner = AddUser(db, "hbo");
        var stranger = AddUser(db, "hbs");

        var hidden = AddBlog(db, owner.UserId, hiding);
        var open = AddBlog(db, owner.UserId, hiding: null);
        await db.SaveChangesAsync();

        return new BlogWorld(Caller(owner), Caller(stranger), hidden, open);
    }

    private static BlogContent AddBlog(DmDbContext db, Guid ownerId, Hiding? hiding)
    {
        var now = DateTimeOffset.UtcNow;
        var content = BlogContent.Nothing();
        db.Blogs.Add(new DbBlog
        {
            BlogId = content.Blog,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = ownerId,
            Title = hiding == null ? "Открытый блог" : "Скрытый блог",
            Description = "Блог для проверки видимости",
            Status = hiding == Hiding.PrivateDraft ? ModuleStatus.Draft : ModuleStatus.Active,
            PremoderationStatus = hiding == Hiding.AwaitingEdits
                ? PremoderationStatus.AwaitingEdits
                : PremoderationStatus.Approved,
            DraftVisibility = hiding == Hiding.PrivateDraft ? DraftVisibility.Private : DraftVisibility.Public,
            CreatedUtc = now.AddDays(-3),
            ActivatedUtc = hiding == Hiding.PrivateDraft ? null : now.AddDays(-2)
        });
        db.Publications.Add(Publication(content.Publication, content.Blog, ownerId, 1, published: true,
            now.AddHours(-2)));
        db.Comments.AddRange(
            new DbComment
            {
                CommentId = content.BlogComment,
                EntityId = content.Blog,
                AuthorId = ownerId,
                CreatedUtc = now.AddHours(-1),
                Text = "Комментарий в блоге"
            },
            new DbComment
            {
                CommentId = content.PublicationComment,
                EntityId = content.Publication,
                AuthorId = ownerId,
                CreatedUtc = now.AddHours(-1),
                Text = "Комментарий под публикацией"
            });
        db.Set<DbNotepadEntry>().Add(new DbNotepadEntry
        {
            EntryId = content.NotepadEntry,
            NotepadType = NotepadType.Blog,
            ContainerId = content.Blog,
            AuthorId = ownerId,
            Title = "Заметка блога",
            Content = "Текст заметки",
            CreatedUtc = now.AddHours(-1)
        });
        return content;
    }

    private static DbPublication Publication(
        Guid publicationId, Guid blogId, Guid authorId, int number, bool published, DateTimeOffset createdUtc) => new()
        {
            PublicationId = publicationId,
            BlogId = blogId,
            PublicationNumber = number,
            AuthorId = authorId,
            Title = published ? "Публикация" : "Черновик публикации",
            Content = "Текст публикации",
            Preview = "Текст",
            IsPublished = published,
            PublishedUtc = published ? createdUtc : null,
            CreatedUtc = createdUtc
        };

    /// <summary>
    /// The hidden game outwrites and outrates the open one inside the period, and
    /// the hidden blog outwrites and outrates the open one: every publication of
    /// the period is liked once.
    /// </summary>
    private async Task DateIntoPeriodAsync(GameWorld games, BlogWorld blogs, DateTimeOffset period)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        foreach (var (content, posts) in new[] { (games.Hidden, 3), (games.Open, 1) })
        {
            for (var i = 0; i < posts; i++)
            {
                db.Set<DbPost>().Add(new DbPost
                {
                    PostId = Guid.NewGuid(),
                    RoomId = content.Room,
                    CharacterId = content.Character,
                    AuthorId = games.Player.UserId,
                    CreatedUtc = period.AddMinutes(i),
                    GameText = "Пост прошлого месяца",
                    PrivateAddresseeSnapshotJson = "{}"
                });
            }
        }

        await db.PostReviews.Where(r => r.PostReviewId == games.Hidden.PostReview || r.PostReviewId == games.Open.PostReview)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.CreatedUtc, period));
        foreach (var (content, publications) in new[] { (blogs.Hidden, 3), (blogs.Open, 1) })
        {
            for (var i = 0; i < publications; i++)
            {
                var publicationId = Guid.NewGuid();
                db.Publications.Add(Publication(publicationId, content.Blog, blogs.Owner.UserId, 10 + i,
                    published: true, period.AddMinutes(i)));
                db.Likes.Add(new DbLike
                {
                    LikeId = Guid.NewGuid(),
                    EntityId = publicationId,
                    EntityType = LikeEntityType.Publication,
                    UserId = blogs.Stranger.UserId
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>The post of the game rated up by as many fresh readers as asked.</summary>
    private async Task<IReadOnlyList<Guid>> RateAsync(GameContent game, Guid postAuthorId, int count)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var rater = AddUser(db, "hmr");
            var id = Guid.NewGuid();
            db.PostReviews.Add(new DbPostReview
            {
                PostReviewId = id,
                AuthorId = rater.UserId,
                PostId = game.Post,
                PostAuthorId = postAuthorId,
                GameId = game.Game,
                CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                SignValue = 1
            });
            ids.Add(id);
        }

        await db.SaveChangesAsync();
        return ids;
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
            Email = $"{userId:N}@hiddenmodule.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        db.Users.Add(user);
        return user;
    }
}
