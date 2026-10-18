using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBlogBlacklist = DM.Infrastructure.Persistence.Entities.Blog.BlogBlacklist;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGameBlacklist = DM.Infrastructure.Persistence.Entities.Game.Links.GameBlacklist;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbLike = DM.Infrastructure.Persistence.Entities.Shared.Like;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostEdit = DM.Infrastructure.Persistence.Entities.Game.Posts.PostEdit;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// What each of the three blacklists does, action by action, through the real
/// routes.
/// </summary>
/// <remarks>
/// The rule, in the owner's words of 05.10: a list closes the minimum and does
/// not do the work of the tools that already exist. The game's list closes two
/// things - the discussion and the application, each with the edit of one's
/// own - and nothing else: who writes in the rooms is decided by the rooms and
/// the roster, and a review, a rating or a like is feedback. The blog's list
/// closes comments, in the blog and under its publications. A topic is a public
/// place, where only a ban keeps somebody out, and a personal list closes one's
/// private messages and hides what one asked it to hide.
///
/// The table is here rather than in the domain suites because the rule used to
/// be kept in many places, and what kept it was the route each request took.
/// Every row sends the request a page would send and reads the answer a page
/// would show, so a closure that comes back in any service turns its row red.
///
/// Each case seeds its own blacklisted user and takes its rows back out; the
/// fixture's game and blog stay as the fixture made them.
/// </remarks>
public class BlacklistRuleShould : IntegrationTestBase
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
    /// The flag stands where something it closes is offered - the game's details
    /// and the blog - and nowhere else. A list of posts from many games, the
    /// game list and a blog's feed offer nothing the lists close, and they used
    /// to carry the flag for the rating form and the heart.
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
    /// <remarks>
    /// The game refused this outright ("remove the user from the game first",
    /// and nothing removes a reader), so the player who had just left - the game
    /// subscribes him by itself - could never be put on the list. The blog took
    /// the subscription away, the one thing the list is not for.
    /// </remarks>
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
            var gamesByHost = await IdsAsync(viewer, $"/v1/games?take=200&hostUsernames={TestConstants.TestUserUsername}");
            var blogs = await IdsAsync(viewer, "/v1/blogs?take=200");
            var chat = await GetAsync(viewer, "/v1/global-chat/messages?limit=100");
            // The search reads the same general chat, and hides the same people:
            // the chat left them out and the search used to hand them back.
            var blockedGreetings = await GlobalChatIdsAsync(TestConstants.TestUserId, "Привет");
            blockedGreetings.Should().NotBeEmpty("the fixture's blocked author greets the chat");
            var found = await IdsAsync(viewer, GreetingSearch);

            games.Should().NotContain(TestConstants.TestGameId, "the viewer hides the games of the people he blocked");
            gamesByHost.Should().Contain(TestConstants.TestGameId, "asked for by the master's name, the list answers in full");
            blogs.Should().NotContain(TestConstants.TestBlogId, "the viewer hides the blogs of the people he blocked");
            chat.GetProperty("resources").EnumerateArray()
                .Select(m => m.GetProperty("author").GetProperty("username").GetString())
                .Should().NotBeEmpty("the general chat of the fixture has other authors")
                .And.NotContain(TestConstants.TestUserUsername, "the viewer hides the messages of the people he blocked");
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
                .Should().Contain(TestConstants.TestUserUsername, "the page the viewer checked shows that author at all");
            (await IdsAsync(viewer, GreetingSearch)).Should().Contain(blockedGreetings,
                "the search the viewer checked finds that author at all");
        }
        finally
        {
            await DropAsync(Seed.Only(viewer));
        }
    }

    // ═══ SEEDING ═══

    /// <summary>
    /// A blacklisted user and what he holds in the game and the blog before the
    /// entries.
    /// </summary>
    /// <remarks>
    /// In the game: a comment, a like under somebody else's comment, a review, a
    /// rating of a post, an application, a character in play and a retired one,
    /// both seated in a room of the case's own where he has written a post, and a
    /// chat room that lets him write as a reader. In the blog: a comment, a
    /// publication of somebody else with a comment of his and one of somebody
    /// else under it, and somebody else's comment in the discussion. The
    /// fixture's master of the game and author of the blog also keeps him on a
    /// personal list, and has a topic with a comment in a board of the case's
    /// own. A review, a rating and the like are left out of the rows that put
    /// the first one: one review per game, one rating per game in three days,
    /// one like per comment.
    /// </remarks>
    private sealed record Seed(GeneralUser User)
    {
        public Guid GameCommentId { get; } = Guid.NewGuid();
        public Guid OtherGameCommentId { get; } = Guid.NewGuid();
        public Guid LikeId { get; } = Guid.NewGuid();
        public Guid ReviewId { get; } = Guid.NewGuid();
        public Guid RatingId { get; } = Guid.NewGuid();
        public Guid ApplicationId { get; } = Guid.NewGuid();
        public Guid CharacterId { get; } = Guid.NewGuid();
        public Guid RetiredCharacterId { get; } = Guid.NewGuid();
        public Guid RoomId { get; } = Guid.NewGuid();
        public Guid PostId { get; } = Guid.NewGuid();
        public Guid ChatRoomId { get; } = Guid.NewGuid();
        public Guid ChatId { get; } = Guid.NewGuid();
        public Guid BlogCommentId { get; } = Guid.NewGuid();
        public Guid OtherBlogCommentId { get; } = Guid.NewGuid();
        public Guid PublicationId { get; } = Guid.NewGuid();
        public Guid PublicationCommentId { get; } = Guid.NewGuid();
        public Guid OtherPublicationCommentId { get; } = Guid.NewGuid();
        public Guid BoardId { get; } = Guid.NewGuid();
        public Guid TopicId { get; } = Guid.NewGuid();
        public Guid TopicCommentId { get; } = Guid.NewGuid();

        /// <summary>A user the case made and nothing else, for the drop.</summary>
        public static Seed Only(GeneralUser user) => new(user);
    }

    private async Task<Seed> SeedBlacklistedAsync(string? action = null)
    {
        // Past the probation, so a review is the newcomer rule's to refuse.
        var user = await SeedUserAsync("blr-x", quantityRating: 200);
        var now = DateTimeOffset.UtcNow;
        var seed = new Seed(user);
        var game = TestConstants.TestGameId;
        var blog = TestConstants.TestBlogId;
        var other = TestConstants.SecondUserId;

        await using var db = DatabaseFixture.CreateDbContext();
        db.GameBlacklists.Add(new DbGameBlacklist
        {
            EntryId = Guid.NewGuid(),
            GameId = game,
            BlockedUserId = user.UserId,
            BlockedByUserId = TestConstants.TestUserId,
            CreatedUtc = now
        });
        db.BlogBlacklists.Add(new DbBlogBlacklist
        {
            EntryId = Guid.NewGuid(),
            BlogId = blog,
            BlockedUserId = user.UserId,
            BlockedByUserId = TestConstants.TestUserId,
            CreatedUtc = now
        });
        db.UserBlacklists.Add(new DbUserBlacklist
        {
            EntryId = Guid.NewGuid(),
            OwnerId = TestConstants.TestUserId,
            BlockedUserId = user.UserId,
            CreatedUtc = now
        });

        // The game.
        db.Comments.AddRange(
            Comment(seed.GameCommentId, game, user.UserId, now),
            Comment(seed.OtherGameCommentId, game, other, now));
        if (action != "like a comment in the game")
        {
            db.Likes.Add(new DbLike
            {
                LikeId = seed.LikeId,
                EntityId = seed.OtherGameCommentId,
                EntityType = LikeEntityType.Comment,
                UserId = user.UserId
            });
        }
        if (action != "review the game")
        {
            db.GameReviews.Add(new DbGameReview
            {
                GameReviewId = seed.ReviewId,
                AuthorId = user.UserId,
                GameId = game,
                CreatedUtc = now,
                Text = "Рецензия, написанная до черного списка",
                IsRemoved = false
            });
        }

        if (action != "rate a post of the game")
        {
            db.PostReviews.Add(new DbPostReview
            {
                PostReviewId = seed.RatingId,
                AuthorId = user.UserId,
                PostId = TestConstants.TestGamePostId,
                PostAuthorId = TestConstants.TestUserId,
                GameId = game,
                CreatedUtc = now,
                Text = null,
                SignValue = 0,
                IsRemoved = false
            });
        }

        db.Set<DbCharacter>().AddRange(
            Character(seed.ApplicationId, user.UserId, "Заявка из черного списка", CharacterStatus.UnderReview, now),
            Character(seed.CharacterId, user.UserId, "Персонаж в игре", CharacterStatus.Active, now),
            Character(seed.RetiredCharacterId, user.UserId, "Ушедший персонаж", CharacterStatus.Retired, now));
        var roomNumber = 1000 + Random.Shared.Next(100_000);
        db.Set<DbRoom>().AddRange(
            new DbRoom
            {
                RoomId = seed.RoomId,
                GameId = game,
                RoomNumber = roomNumber,
                Title = "Комната внесенного",
                AccessType = RoomAccessType.Open,
                Type = RoomType.Default,
                OrderNumber = 1000.0,
                IsRemoved = false
            },
            new DbRoom
            {
                RoomId = seed.ChatRoomId,
                GameId = game,
                RoomNumber = roomNumber + 1,
                Title = "Чат-комната внесенного",
                AccessType = RoomAccessType.Private,
                Type = RoomType.Chat,
                OrderNumber = 1001.0,
                ChatId = seed.ChatId,
                IsRemoved = false
            });
        db.Chats.Add(new DbChat
        {
            ChatId = seed.ChatId,
            Type = ChatType.GameRoom,
            Title = "Чат-комната внесенного",
            RoomId = seed.ChatRoomId,
            PublicId = $"b{seed.ChatId:N}"[..10]
        });
        db.Set<DbRoomAccess>().AddRange(
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                CharacterId = seed.CharacterId,
                RoomId = seed.RoomId,
                Policy = RoomAccessPolicy.Full
            },
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                CharacterId = seed.RetiredCharacterId,
                RoomId = seed.RoomId,
                Policy = RoomAccessPolicy.Full
            },
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                ReaderUserId = user.UserId,
                RoomId = seed.ChatRoomId,
                Policy = RoomAccessPolicy.Full
            });
        db.Set<DbPost>().Add(new DbPost
        {
            PostId = seed.PostId,
            RoomId = seed.RoomId,
            CharacterId = seed.CharacterId,
            AuthorId = user.UserId,
            CreatedUtc = now.AddMinutes(-1),
            GameText = "Пост, написанный до черного списка",
            PrivateAddresseeSnapshotJson = "{}",
            IsRemoved = false
        });

        // The blog.
        db.Publications.Add(new DbPublication
        {
            PublicationId = seed.PublicationId,
            BlogId = blog,
            PublicationNumber = 9000 + Random.Shared.Next(100_000),
            AuthorId = TestConstants.TestUserId,
            Title = "Публикация в блоге со списком",
            Content = "Текст",
            Preview = "Текст",
            IsPublished = true,
            PublishedUtc = now,
            CreatedUtc = now,
            IsRemoved = false
        });
        db.Comments.AddRange(
            Comment(seed.BlogCommentId, blog, user.UserId, now),
            Comment(seed.OtherBlogCommentId, blog, other, now),
            Comment(seed.PublicationCommentId, seed.PublicationId, user.UserId, now),
            Comment(seed.OtherPublicationCommentId, seed.PublicationId, other, now));

        // The forum.
        db.Boards.Add(new DbBoard
        {
            BoardId = seed.BoardId,
            Title = $"Blacklist board {seed.BoardId:N}",
            Alias = $"blr-{seed.BoardId:N}",
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.Guest
        });
        db.Topics.Add(new DbTopic
        {
            TopicId = seed.TopicId,
            BoardId = seed.BoardId,
            AuthorId = TestConstants.TestUserId,
            Title = "Топик того, кто внес",
            Text = "Текст топика того, кто внес",
            TopicNumber = 1,
            CreatedUtc = now.AddHours(-1),
            IsRemoved = false,
            IsAttached = false,
            IsClosed = false
        });
        db.Comments.Add(Comment(seed.TopicCommentId, seed.TopicId, TestConstants.TestUserId, now));
        await db.SaveChangesAsync();
        return seed;
    }

    private async Task<GeneralUser> SeedUserAsync(
        string prefix, UserBlacklistSettings settings = UserBlacklistSettings.Default, int quantityRating = 0)
    {
        var userId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@blacklistrule.example",
            PasswordHash = "hash",
            Salt = "salt",
            BlacklistSettings = settings,
            QuantityRating = quantityRating
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new GeneralUser { UserId = userId, Username = user.Username, Role = UserRole.RegularUser };
    }

    private static DbComment Comment(Guid commentId, Guid entityId, Guid authorId, DateTimeOffset now) => new()
    {
        CommentId = commentId,
        EntityId = entityId,
        AuthorId = authorId,
        CreatedUtc = now.AddMinutes(-1),
        Text = "Комментарий, написанный до черного списка",
        IsRemoved = false
    };

    private static DbCharacter Character(
        Guid characterId, Guid authorId, string name, CharacterStatus status, DateTimeOffset now) => new()
        {
            CharacterId = characterId,
            GameId = TestConstants.TestGameId,
            AuthorId = authorId,
            Name = name,
            Status = status,
            IsPlayerLeft = status == CharacterStatus.Retired,
            IsNpc = false,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = now.AddDays(-1),
            IsRemoved = false
        };

    private static DbSubscription Subscription(Guid subscriberId, SubscriptionTargetType type, Guid targetId) => new()
    {
        SubscriptionId = Guid.NewGuid(),
        SubscriberId = subscriberId,
        TargetType = type,
        TargetId = targetId,
        Settings = SubscriptionSettings.InApp,
        CreatedUtc = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Everything the case wrote, or its requests wrote, under the user it made.
    /// The user row stays, the way the neighbouring suites leave theirs.
    /// </summary>
    private async Task DropAsync(Seed seed)
    {
        var userId = seed.User.UserId;
        var rooms = new[] { seed.RoomId, seed.ChatRoomId };
        var commented = new[] { seed.OtherGameCommentId, seed.OtherBlogCommentId, seed.OtherPublicationCommentId };
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Likes.IgnoreQueryFilters()
            .Where(l => l.UserId == userId || commented.Contains(l.EntityId)).ExecuteDeleteAsync();
        await db.Topics.IgnoreQueryFilters().Where(t => t.TopicId == seed.TopicId)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.LastCommentId, (Guid?)null));
        await db.Comments.IgnoreQueryFilters()
            .Where(c => c.AuthorId == userId || commented.Contains(c.CommentId) || c.EntityId == seed.TopicId ||
                        c.EntityId == seed.PublicationId)
            .ExecuteDeleteAsync();
        await db.Publications.IgnoreQueryFilters().Where(p => p.PublicationId == seed.PublicationId)
            .ExecuteDeleteAsync();
        await db.Set<DbPostPendency>().Where(p => rooms.Contains(p.RoomId)).ExecuteDeleteAsync();
        await db.Set<DbPostEdit>().Where(e => db.Set<DbPost>().IgnoreQueryFilters()
            .Any(p => p.PostId == e.PostId && p.AuthorId == userId)).ExecuteDeleteAsync();
        await db.PostReviews.IgnoreQueryFilters().Where(r => r.AuthorId == userId).ExecuteDeleteAsync();
        await db.Set<DbPost>().IgnoreQueryFilters().Where(post => post.AuthorId == userId).ExecuteDeleteAsync();
        await db.Set<DbRoomAccess>().Where(a => rooms.Contains(a.RoomId)).ExecuteDeleteAsync();
        await db.Chats.Where(c => c.ChatId == seed.ChatId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.LastMessageId, (Guid?)null));
        await db.Set<DbMessage>().IgnoreQueryFilters().Where(m => m.ChatId == seed.ChatId).ExecuteDeleteAsync();
        await db.Set<DbRoom>().IgnoreQueryFilters().Where(r => rooms.Contains(r.RoomId)).ExecuteDeleteAsync();
        await db.Chats.Where(c => c.ChatId == seed.ChatId).ExecuteDeleteAsync();
        await db.Set<DbCharacter>().IgnoreQueryFilters().Where(c => c.AuthorId == userId).ExecuteDeleteAsync();
        await db.Boards.Where(b => b.BoardId == seed.BoardId)
            .ExecuteUpdateAsync(b => b.SetProperty(x => x.LastTopicId, (Guid?)null));
        await db.Topics.IgnoreQueryFilters().Where(t => t.BoardId == seed.BoardId).ExecuteDeleteAsync();
        await db.Boards.Where(b => b.BoardId == seed.BoardId).ExecuteDeleteAsync();
        await db.GameReviews.IgnoreQueryFilters().Where(r => r.AuthorId == userId).ExecuteDeleteAsync();
        await db.Tokens.IgnoreQueryFilters().Where(t => t.UserId == userId).ExecuteDeleteAsync();
        await db.GameBlacklists.Where(b => b.BlockedUserId == userId).ExecuteDeleteAsync();
        await db.BlogBlacklists.Where(b => b.BlockedUserId == userId).ExecuteDeleteAsync();
        await db.UserBlacklists.Where(b => b.BlockedUserId == userId || b.OwnerId == userId).ExecuteDeleteAsync();
        await db.Subscriptions.Where(s => s.SubscriberId == userId).ExecuteDeleteAsync();
    }

    // ═══ REQUESTS ═══

    private Task<HttpResponseMessage> SendAsync(string action, Seed seed)
    {
        var game = TestConstants.TestGameId;
        var blog = TestConstants.TestBlogId;
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
            "review the game" => Send(seed.User, HttpMethod.Post, $"/v1/games/{game}/reviews", new { text }),
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
            "be invited into the blog by its author" => Send(master, HttpMethod.Post,
                $"/v1/blogs/{blog}/invitations/readers", new { username = seed.User.Username }),
            "edit one's blog comment through the forum route" => Send(seed.User, HttpMethod.Patch,
                $"/v1/forum/comments/{seed.BlogCommentId}", new { text }),
            "edit one's blog comment through the game route" => Send(seed.User, HttpMethod.Patch,
                $"/v1/games/comments/{seed.BlogCommentId}", new { text }),
            "comment in the topic of somebody who blocked you" => Send(seed.User, HttpMethod.Post,
                $"/v1/topics/{seed.TopicId}/comments", new { text }),
            "like the topic of somebody who blocked you" => Send(seed.User, HttpMethod.Post,
                $"/v1/topics/{seed.TopicId}/likes"),
            "like a comment of somebody who blocked you in the forum" => Send(seed.User, HttpMethod.Post,
                $"/v1/forum/comments/{seed.TopicCommentId}/likes"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "no request for this row")
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
            .Where(m => m.ChatId == DbChat.GlobalChatId && m.UserId == authorId && !m.IsRemoved &&
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
