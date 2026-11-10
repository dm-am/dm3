using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;

namespace DM.Web.API.IntegrationTests.Controllers.General;

// Each case runs under a private draft and under premoderation, and beside every hidden module
// stands an open twin, on which the stranger has to get something else.
public partial class HiddenModuleShould : IntegrationTestBase
{
    public HiddenModuleShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    public enum Hiding
    {
        PrivateDraft,
        AwaitingEdits
    }

    public static TheoryData<Hiding> Hidings => new() { Hiding.PrivateDraft, Hiding.AwaitingEdits };

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

    // The boards of a past month leave a hidden game or blog out, even when it outwrote the open
    // one.
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

    // The best post of the week is a post a guest can read: the hidden one is rated higher and
    // still is not named.
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

    // Status and body together: the body is what tells two 404s apart, and two lists that both
    // answer 200, one of them with what the hidden module holds.
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
        return new Answer(response.StatusCode, Masked(await response.Content.ReadAsStringAsync()));
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
}
