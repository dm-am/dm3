using DM.Domain.Core.Chats;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// What each of the three blacklists does, action by action, through the real
/// routes.
/// </summary>
/// <remarks>
/// A list closes the minimum: the game's the discussion and the application, the blog's comments; a topic is
/// public, and a personal list closes private messages and hides what it was asked to. Every row sends the
/// request a page would send; each case seeds its own blacklisted user.
/// </remarks>
public partial class BlacklistRuleShould : IntegrationTestBase
{
    public BlacklistRuleShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>What the list does to one action.</summary>
    public enum Outcome
    {
        /// <summary>The action goes through.</summary>
        Allowed,

        /// <summary>Refused in the words of the game's list.</summary>
        RefusedByGameList,

        /// <summary>Refused in the words of the blog's list.</summary>
        RefusedByBlogList,

        /// <summary>Refused by another rule, and not in the words of any list.</summary>
        RefusedByAnotherRule,

        /// <summary>The comment is not there to this route.</summary>
        NotFound
    }

    public static TheoryData<string, Outcome> Actions() => new()
    {
        // The game's list closes the discussion and the application, each with
        // the edit of one's own...
        { "comment in the game", Outcome.RefusedByGameList },
        { "edit one's comment in the game", Outcome.RefusedByGameList },
        { "apply to the game", Outcome.RefusedByGameList },
        { "edit one's application", Outcome.RefusedByGameList },
        // ...and nothing else.
        { "subscribe to the game", Outcome.Allowed },
        { "subscribe to the game through the generic endpoint", Outcome.Allowed },
        { "read the game", Outcome.Allowed },
        { "like a comment in the game", Outcome.Allowed },
        { "take back one's like in the game", Outcome.Allowed },
        { "review the game", Outcome.Allowed },
        { "edit one's review of the game", Outcome.Allowed },
        { "rate a post of the game", Outcome.Allowed },
        { "edit one's rating of a post", Outcome.Allowed },
        { "post in a room as one's character", Outcome.Allowed },
        { "edit one's post", Outcome.Allowed },
        { "write in the chat room", Outcome.Allowed },
        { "mark whose turn it is", Outcome.Allowed },
        { "edit the sheet of one's character in play", Outcome.Allowed },
        { "be invited into the game by its master", Outcome.Allowed },
        // Who writes in a room is the room's and the roster's to say: the access
        // rows of a retired character stay, and they open nothing.
        { "post in a room as one's retired character", Outcome.RefusedByAnotherRule },
        // The blog's list closes comments...
        { "comment in the blog", Outcome.RefusedByBlogList },
        { "edit one's comment in the blog", Outcome.RefusedByBlogList },
        { "comment under a publication of the blog", Outcome.RefusedByBlogList },
        { "edit one's comment under a publication", Outcome.RefusedByBlogList },
        // ...and nothing else.
        { "subscribe to the blog", Outcome.Allowed },
        { "subscribe to the blog through the generic endpoint", Outcome.Allowed },
        { "read the blog", Outcome.Allowed },
        { "like a publication of the blog", Outcome.Allowed },
        { "like a comment in the blog", Outcome.Allowed },
        { "like a comment under a publication", Outcome.Allowed },
        { "be invited into the blog by its author", Outcome.Allowed },
        // An edit cannot walk around a list through another discussion's route.
        { "edit one's blog comment through the forum route", Outcome.NotFound },
        { "edit one's blog comment through the game route", Outcome.NotFound },
        // A personal list does not close a public place.
        { "comment in the topic of somebody who blocked you", Outcome.Allowed },
        { "like the topic of somebody who blocked you", Outcome.Allowed },
        { "like a comment of somebody who blocked you in the forum", Outcome.Allowed },
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task DoToEachActionWhatTheOwnerDecided(string action, Outcome expected)
    {
        var seed = await SeedBlacklistedAsync(action);
        try
        {
            var response = await SendAsync(action, seed);
            var body = await response.Content.ReadAsStringAsync();

            switch (expected)
            {
                case Outcome.Allowed:
                    response.IsSuccessStatusCode.Should().BeTrue($"'{action}' is open to a blacklisted user: {body}");
                    break;
                case Outcome.RefusedByGameList:
                    response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
                    TitleOf(body).Should().Be(RefusalMessage.BlacklistedFromGame);
                    break;
                case Outcome.RefusedByBlogList:
                    response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
                    TitleOf(body).Should().Be(RefusalMessage.BlacklistedFromBlog);
                    break;
                case Outcome.RefusedByAnotherRule:
                    response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
                    TitleOf(body).Should().NotBe(RefusalMessage.BlacklistedFromGame, body);
                    break;
                case Outcome.NotFound:
                    response.StatusCode.Should().Be(HttpStatusCode.NotFound, body);
                    break;
            }

            if (action is "read the game" or "read the blog")
            {
                // What the page leaves the comment form out by.
                using var document = JsonDocument.Parse(body);
                document.RootElement.GetProperty("resource").GetProperty("viewerBlacklisted").GetBoolean()
                    .Should().BeTrue();
            }
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// The flag the page leaves the closed forms out by is about the viewer: true
    /// for the person on the list, false for anybody else and for a guest.
    /// </summary>
    [Fact]
    public async Task TellOnlyTheBlacklistedViewerThatHeIsOnTheList()
    {
        var seed = await SeedBlacklistedAsync();
        try
        {
            var stranger = CustomWebApplicationFactory.CreateSecondUser();
            foreach (var url in new[]
                     {
                         $"/v1/games/{TestConstants.TestGameId}/details", $"/v1/blogs/{TestConstants.TestBlogId}"
                     })
            {
                (await FlagAsync(seed.User, url)).Should().BeTrue(url);
                (await FlagAsync(stranger, url)).Should().BeFalse(url);
                (await FlagAsync(null, url)).Should().BeFalse(url);
            }
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// The flag stands where something it closes is offered, the game's details and the blog, and nowhere else.
    /// </summary>
    [Fact]
    public async Task KeepTheFlagOffTheShapesThatOfferNothingTheListsClose()
    {
        var seed = await SeedBlacklistedAsync();
        try
        {
            var posts = await GetAsync(seed.User, $"/v1/posts?gameId={TestConstants.TestGameId}&take=100");
            var postGames = posts.GetProperty("resources").EnumerateArray()
                .Select(p => p.GetProperty("room").GetProperty("game"))
                .ToArray();
            postGames.Should().NotBeEmpty("the fixture's game has a post");
            postGames.Should().AllSatisfy(game => game.TryGetProperty("viewerBlacklisted", out _).Should().BeFalse());

            var games = (await GetAsync(seed.User, "/v1/games?take=200")).GetProperty("resources").EnumerateArray()
                .ToArray();
            games.Should().Contain(game => game.GetProperty("id").GetGuid() == TestConstants.TestGameId);
            games.Should().AllSatisfy(game => game.TryGetProperty("viewerBlacklisted", out _).Should().BeFalse());

            var feed = (await GetAsync(seed.User, $"/v1/blogs/{TestConstants.TestBlogId}/publications"))
                .GetProperty("resources").EnumerateArray().ToArray();
            feed.Should().Contain(p => p.GetProperty("id").GetGuid() == seed.PublicationId);
            feed.Should().AllSatisfy(p => p.TryGetProperty("viewerBlacklisted", out _).Should().BeFalse());
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// The owner blacklists a subscriber, and the subscription stays.
    /// </summary>
    /// <remarks>The game subscribes a player who leaves by itself, and the list is not for subscriptions.</remarks>
    [Fact]
    public async Task BlacklistASubscriberAndKeepTheSubscription()
    {
        var subscriber = await SeedUserAsync("blr-sub");
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Subscriptions.AddRange(
                Subscription(subscriber.UserId, SubscriptionTargetType.Game, TestConstants.TestGameId),
                Subscription(subscriber.UserId, SubscriptionTargetType.Blog, TestConstants.TestBlogId));
            await db.SaveChangesAsync();
        }

        try
        {
            var owner = CustomWebApplicationFactory.CreateTestUser();
            var toGame = CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/games/{TestConstants.TestGameId}/blacklist", owner);
            toGame.Content = JsonContent.Create(new { username = subscriber.Username });
            var toBlog = CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/blogs/{TestConstants.TestBlogId}/blacklist", owner);
            toBlog.Content = JsonContent.Create(new { username = subscriber.Username });

            (await Client.SendAsync(toGame)).StatusCode.Should().Be(HttpStatusCode.Created);
            (await Client.SendAsync(toBlog)).StatusCode.Should().Be(HttpStatusCode.Created);

            await using var check = DatabaseFixture.CreateDbContext();
            (await check.GameBlacklists.AnyAsync(b =>
                b.GameId == TestConstants.TestGameId && b.BlockedUserId == subscriber.UserId)).Should().BeTrue();
            (await check.BlogBlacklists.AnyAsync(b =>
                b.BlogId == TestConstants.TestBlogId && b.BlockedUserId == subscriber.UserId)).Should().BeTrue();
            (await check.Subscriptions.CountAsync(s => s.SubscriberId == subscriber.UserId))
                .Should().Be(2, "the list closes the discussion and the application, and a subscription is reading");
        }
        finally
        {
            await DropAsync(Seed.Only(subscriber));
        }
    }

    /// <summary>
    /// "Скрывать игры", "Скрывать блоги" and "Скрывать сообщения в чате": the
    /// games, blogs and general chat messages of the people on the viewer's list
    /// leave what the viewer browses, and a list asked for by that person's name
    /// still answers in full.
    /// </summary>
    [Fact]
    public async Task HideWhatTheViewerAskedToHide()
    {
        var viewer = await SeedUserAsync("blr-viewer",
            UserBlacklistSettings.HideGames | UserBlacklistSettings.HideBlogs | UserBlacklistSettings.HideMessages);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.UserBlacklists.Add(new DbUserBlacklist
            {
                EntryId = Guid.NewGuid(),
                OwnerId = viewer.UserId,
                BlockedUserId = TestConstants.TestUserId,
                CreatedUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        try
        {
            var games = await IdsAsync(viewer, "/v1/games?take=200");
            var gamesByHost = await IdsAsync(viewer, $"/v1/games?take=200&hostUsernames={TestConstants.TestUserLogin}");
            var blogs = await IdsAsync(viewer, "/v1/blogs?take=200");
            var chat = await GetAsync(viewer, "/v1/global-chat/messages?limit=100");
            // The search reads the same general chat and hides the same people.
            var blockedGreetings = await GlobalChatIdsAsync(TestConstants.TestUserId, "Привет");
            blockedGreetings.Should().NotBeEmpty("the fixture's blocked author greets the chat");
            var found = await IdsAsync(viewer, GreetingSearch);

            games.Should().NotContain(TestConstants.TestGameId, "the viewer hides the games of the people he blocked");
            gamesByHost.Should().Contain(TestConstants.TestGameId, "asked for by the master's name, the list answers in full");
            blogs.Should().NotContain(TestConstants.TestBlogId, "the viewer hides the blogs of the people he blocked");
            chat.GetProperty("resources").EnumerateArray()
                .Select(m => m.GetProperty("author").GetProperty("username").GetString())
                .Should().NotBeEmpty("the general chat of the fixture has other authors")
                .And.NotContain(TestConstants.TestUserLogin, "the viewer hides the messages of the people he blocked");
            found.Should().NotBeEmpty("another author greets the chat too")
                .And.NotContain(blockedGreetings, "the search of the general chat hides whom the chat hides");

            // The switches are what hide: the same list without them hides nothing.
            await using (var db = DatabaseFixture.CreateDbContext())
            {
                await db.Users.Where(u => u.UserId == viewer.UserId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.BlacklistSettings, UserBlacklistSettings.None));
            }

            (await IdsAsync(viewer, "/v1/games?take=200")).Should().Contain(TestConstants.TestGameId);
            (await IdsAsync(viewer, "/v1/blogs?take=200")).Should().Contain(TestConstants.TestBlogId);
            (await GetAsync(viewer, "/v1/global-chat/messages?limit=100")).GetProperty("resources").EnumerateArray()
                .Select(m => m.GetProperty("author").GetProperty("username").GetString())
                .Should().Contain(TestConstants.TestUserLogin, "the page the viewer checked shows that author at all");
            (await IdsAsync(viewer, GreetingSearch)).Should().Contain(blockedGreetings,
                "the search the viewer checked finds that author at all");
        }
        finally
        {
            await DropAsync(Seed.Only(viewer));
        }
    }

    /// <summary>The request a page sends for one row, asked of the list the row is about.</summary>
    private Task<HttpResponseMessage> SendAsync(string action, Seed seed) =>
        GameRequest(action, seed) ?? BlogRequest(action, seed) ?? ForumRequest(action, seed)
        ?? throw new ArgumentOutOfRangeException(nameof(action), action, "no request for this row");

    /// <summary>
    /// A review is written on a closed game alone, so the shared game is closed for the one request and
    /// opened again after it.
    /// </summary>
    private async Task<HttpResponseMessage> WhileTheGameIsClosed(Func<Task<HttpResponseMessage>> send)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var games = db.Games.Where(g => g.GameId == TestConstants.TestGameId);
        var was = await games.Select(g => new { g.Status, g.ClosedReason }).SingleAsync();
        await games.ExecuteUpdateAsync(g => g
            .SetProperty(x => x.Status, ModuleStatus.Closed)
            .SetProperty(x => x.ClosedReason, ClosedReason.Finished));
        try
        {
            return await send();
        }
        finally
        {
            await games.ExecuteUpdateAsync(g => g
                .SetProperty(x => x.Status, was.Status)
                .SetProperty(x => x.ClosedReason, was.ClosedReason));
        }
    }

    /// <summary>The rows of the game's list, or null for a row of another list.</summary>
    private Task<HttpResponseMessage>? GameRequest(string action, Seed seed)
    {
        var game = TestConstants.TestGameId;
        var master = CustomWebApplicationFactory.CreateTestUser();
        const string text = "Текст, который пишет пользователь из черного списка";

        return action switch
        {
            "comment in the game" => Send(seed.User, HttpMethod.Post, $"/v1/games/{game}/comments", new { text }),
            "edit one's comment in the game" => Send(seed.User, HttpMethod.Patch,
                $"/v1/games/comments/{seed.GameCommentId}", new { text }),
            "apply to the game" => Send(seed.User, HttpMethod.Post, $"/v1/games/{game}/characters",
                new { name = "Персонаж из черного списка", attributes = Array.Empty<object>(), privacy = new { } }),
            "edit one's application" => Send(seed.User, HttpMethod.Patch, $"/v1/characters/{seed.ApplicationId}",
                new { name = "Заявка, поправленная из черного списка" }),
            "subscribe to the game" => Send(seed.User, HttpMethod.Post, $"/v1/games/{game}/readers"),
            "subscribe to the game through the generic endpoint" => Send(seed.User, HttpMethod.Post,
                "/v1/users/me/subscriptions", new { targetType = "Game", targetId = game }),
            "read the game" => Send(seed.User, HttpMethod.Get, $"/v1/games/{game}/details"),
            "like a comment in the game" => Send(seed.User, HttpMethod.Post,
                $"/v1/games/comments/{seed.OtherGameCommentId}/likes"),
            "take back one's like in the game" => Send(seed.User, HttpMethod.Delete,
                $"/v1/games/comments/{seed.OtherGameCommentId}/likes"),
            "review the game" => WhileTheGameIsClosed(() =>
                Send(seed.User, HttpMethod.Post, $"/v1/games/{game}/reviews", new { text })),
            "edit one's review of the game" => Send(seed.User, HttpMethod.Patch,
                $"/v1/games/{game}/reviews/{seed.ReviewId}", new { text }),
            "rate a post of the game" => Send(seed.User, HttpMethod.Post,
                $"/v1/posts/{TestConstants.TestGamePostId}/reviews", new { sign = "Neutral", text }),
            "edit one's rating of a post" => Send(seed.User, HttpMethod.Patch,
                $"/v1/posts/{TestConstants.TestGamePostId}/reviews/{seed.RatingId}", new { sign = "Neutral", text }),
            "post in a room as one's character" => Send(seed.User, HttpMethod.Post,
                $"/v1/rooms/{seed.RoomId}/posts", new { characterId = seed.CharacterId, gameText = text }),
            "edit one's post" => Send(seed.User, HttpMethod.Patch, $"/v1/posts/{seed.PostId}", new { gameText = text }),
            "write in the chat room" => Send(seed.User, HttpMethod.Post,
                $"/v1/chat-rooms/{seed.ChatRoomId}/messages", new { text }),
            // The body carries createdBy because the create model still asks for
            // it; the server writes the caller there whatever it says.
            "mark whose turn it is" => Send(seed.User, HttpMethod.Post, $"/v1/rooms/{seed.RoomId}/pendencies",
                new
                {
                    characterId = seed.CharacterId,
                    createdBy = new { username = seed.User.Username },
                    waitingFor = new { username = seed.User.Username }
                }),
            "edit the sheet of one's character in play" => Send(seed.User, HttpMethod.Patch,
                $"/v1/characters/{seed.CharacterId}", new { name = "Персонаж, поправленный из черного списка" }),
            "be invited into the game by its master" => Send(master, HttpMethod.Post,
                $"/v1/games/{game}/invitations/players", new { username = seed.User.Username }),
            "post in a room as one's retired character" => Send(seed.User, HttpMethod.Post,
                $"/v1/rooms/{seed.RoomId}/posts", new { characterId = seed.RetiredCharacterId, gameText = text }),
            _ => null
        };
    }

    /// <summary>The rows of the blog's list, or null for a row of another list.</summary>
    private Task<HttpResponseMessage>? BlogRequest(string action, Seed seed)
    {
        var blog = TestConstants.TestBlogId;
        var author = CustomWebApplicationFactory.CreateTestUser();
        const string text = "Текст, который пишет пользователь из черного списка";

        return action switch
        {
            "comment in the blog" => Send(seed.User, HttpMethod.Post, $"/v1/blogs/{blog}/comments", new { text }),
            "edit one's comment in the blog" => Send(seed.User, HttpMethod.Patch,
                $"/v1/blogs/comments/{seed.BlogCommentId}", new { text }),
            "comment under a publication of the blog" => Send(seed.User, HttpMethod.Post,
                $"/v1/publications/{seed.PublicationId}/comments", new { text }),
            "edit one's comment under a publication" => Send(seed.User, HttpMethod.Patch,
                $"/v1/publications/comments/{seed.PublicationCommentId}", new { text }),
            "subscribe to the blog" => Send(seed.User, HttpMethod.Post, $"/v1/blogs/{blog}/readers"),
            "subscribe to the blog through the generic endpoint" => Send(seed.User, HttpMethod.Post,
                "/v1/users/me/subscriptions", new { targetType = "Blog", targetId = blog }),
            "read the blog" => Send(seed.User, HttpMethod.Get, $"/v1/blogs/{blog}"),
            "like a publication of the blog" => Send(seed.User, HttpMethod.Post,
                $"/v1/publications/{seed.PublicationId}/likes"),
            "like a comment in the blog" => Send(seed.User, HttpMethod.Post,
                $"/v1/blogs/comments/{seed.OtherBlogCommentId}/likes"),
            "like a comment under a publication" => Send(seed.User, HttpMethod.Post,
                $"/v1/publications/comments/{seed.OtherPublicationCommentId}/likes"),
            "be invited into the blog by its author" => Send(author, HttpMethod.Post,
                $"/v1/blogs/{blog}/invitations/readers", new { username = seed.User.Username }),
            "edit one's blog comment through the forum route" => Send(seed.User, HttpMethod.Patch,
                $"/v1/forum/comments/{seed.BlogCommentId}", new { text }),
            "edit one's blog comment through the game route" => Send(seed.User, HttpMethod.Patch,
                $"/v1/games/comments/{seed.BlogCommentId}", new { text }),
            _ => null
        };
    }

    /// <summary>The rows of a personal list in the forum, or null for a row of another list.</summary>
    private Task<HttpResponseMessage>? ForumRequest(string action, Seed seed)
    {
        const string text = "Текст, который пишет пользователь из черного списка";

        return action switch
        {
            "comment in the topic of somebody who blocked you" => Send(seed.User, HttpMethod.Post,
                $"/v1/topics/{seed.TopicId}/comments", new { text }),
            "like the topic of somebody who blocked you" => Send(seed.User, HttpMethod.Post,
                $"/v1/topics/{seed.TopicId}/likes"),
            "like a comment of somebody who blocked you in the forum" => Send(seed.User, HttpMethod.Post,
                $"/v1/forum/comments/{seed.TopicCommentId}/likes"),
            _ => null
        };
    }

    private Task<HttpResponseMessage> Send(GeneralUser user, HttpMethod method, string url, object? body = null)
    {
        var request = CreateAuthenticatedRequest(method, url, user);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return Client.SendAsync(request);
    }

    private async Task<JsonElement> GetAsync(GeneralUser user, string url)
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, user));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<bool> FlagAsync(GeneralUser? user, string url)
    {
        var response = user is null
            ? await Client.GetAsync(url)
            : await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, user));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("resource").GetProperty("viewerBlacklisted").GetBoolean();
    }

    /// <summary>A search of the general chat for a word two fixture authors wrote.</summary>
    private static readonly string GreetingSearch =
        $"/v1/search/messages?search={Uri.EscapeDataString("Привет")}&in=global&limit=100";

    /// <summary>The general chat messages of one author that carry the word.</summary>
    private async Task<IReadOnlyCollection<Guid>> GlobalChatIdsAsync(Guid authorId, string word)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.Set<DbMessage>()
            .Where(m => m.ChatId == WellKnownChats.GlobalChatId && m.UserId == authorId && !m.IsRemoved &&
                        m.Text.Contains(word))
            .Select(m => m.MessageId)
            .ToArrayAsync();
    }

    private async Task<IReadOnlyCollection<Guid>> IdsAsync(GeneralUser user, string url) =>
        (await GetAsync(user, url)).GetProperty("resources").EnumerateArray()
        .Select(item => item.GetProperty("id").GetGuid())
        .ToArray();

    private static string? TitleOf(string problem)
    {
        using var document = JsonDocument.Parse(problem);
        return document.RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;
    }
}
