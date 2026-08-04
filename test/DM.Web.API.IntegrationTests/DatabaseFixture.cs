using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Shared fixture managing PostgreSQL, MongoDB, and RabbitMQ containers.
/// Provides consistent test data seeding with a data-driven approach.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    // The image the stand runs, not the small one. Alpine carries no locales, so
    // initdb falls back to C and ORDER BY over a name becomes byte order: every
    // capital before every lower-case letter, all of Cyrillic after all of Latin.
    // The stand is postgres:16 in en_US.utf8 and orders the same names the way a
    // reader expects, so a list assertion here answered a question production
    // never asks. The locale is spelled out rather than left to the image default
    // for the same reason the image is: an ordering is only testable against the
    // collation it will actually run under.
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithEnvironment("POSTGRES_INITDB_ARGS", "--locale=en_US.utf8")
        .WithDatabase("dm3_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
        .WithImage("mongo:7")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private CustomWebApplicationFactory? _sharedFactory;

    public string ConnectionString { get; private set; } = string.Empty;
    public string MongoConnectionString { get; private set; } = string.Empty;
    public string RabbitMqConnectionString { get; private set; } = string.Empty;
    public bool IsSeeded { get; private set; }

    public CustomWebApplicationFactory Factory => _sharedFactory ??= new CustomWebApplicationFactory(this);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _mongoContainer.StartAsync(),
            _rabbitMqContainer.StartAsync());

        ConnectionString = _postgresContainer.GetConnectionString();

        var rawMongoUrl = _mongoContainer.GetConnectionString();
        MongoConnectionString = rawMongoUrl.Contains("/?")
            ? rawMongoUrl.Replace("/?", "/dm3_test?authSource=admin&")
            : rawMongoUrl.TrimEnd('/') + "/dm3_test";

        RabbitMqConnectionString = _rabbitMqContainer.GetConnectionString();

        await using var context = CreateDbContext();
        // The migration builds the schema, not the model. This is the database the
        // site actually starts from: InitialCreate seeds boards, tag groups, tags,
        // topics, achievement and award types, the contest series and the system
        // user, and nothing else ever exercises the application against that
        // starting state. Building from the model instead would silently drop all
        // of it, and would also let the migration rot while the tests stayed green.
        // The migration creates the pg_trgm extension itself, before the indexes
        // that need it, so no preparation is required here.
        await context.Database.MigrateAsync();
        await SeedAllAsync(context);
        IsSeeded = true;
    }

    public async Task DisposeAsync()
    {
        _sharedFactory?.Dispose();
        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _mongoContainer.DisposeAsync().AsTask(),
            _rabbitMqContainer.DisposeAsync().AsTask());
    }

    public DmDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseNpgsql(ConnectionString)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors()
        .Options);

    /// <summary>
    /// Context for another database on the same container, for tests that need a schema
    /// built by the migration itself and untouched by the seed. The caller creates the
    /// database with MigrateAsync and drops it in a finally.
    /// </summary>
    public DmDbContext CreateDbContextFor(string databaseName)
    {
        var connection = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = databaseName,
        };
        return new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql(connection.ConnectionString)
            .Options);
    }

    #region Seeding

    // Every guard below keys on a row this fixture owns, never on a bare Any():
    // InitialCreate ships rows of its own (see MigrateAsync above), and a bare
    // Any() would silently skip the whole seeder the moment the migration grows a
    // row of the same kind, taking every test that depends on it down with an
    // error that names no cause.
    private static async Task SeedAllAsync(DmDbContext db)
    {
        // Order matters: FK constraints
        SeedUsers(db);
        await db.SaveChangesAsync();

        SeedBoards(db);
        SeedTopics(db);
        SeedGames(db);
        SeedBlogs(db);
        SeedRooms(db);
        SeedCharacters(db);
        SeedGamePosts(db);
        SeedChats(db);
        SeedTestimonials(db);
        SeedBoardModerators(db);
        SeedSubscriptions(db);
        await db.SaveChangesAsync();

        SeedGameTags(db);
        await db.SaveChangesAsync();
    }

    private static void SeedUsers(DmDbContext db)
    {
        if (db.Users.Any(u => u.UserId == TestConstants.TestUserId)) return;

        // (Id, Username, Email, Role, DaysAgo, InactiveDays)
        // InactiveDays: null = active (LastActivityUtc = now), otherwise = days since last activity
        var coreUsers = new (Guid Id, string Username, string Email, UserRole Role, int DaysAgo, int? InactiveDays)[]
        {
            (TestConstants.TestUserId, TestConstants.TestUserUsername, "test@example.com", UserRole.RegularUser, 30, null),
            (TestConstants.AdminUserId, TestConstants.AdminUserUsername, "admin@example.com", UserRole.Admin, 365, null),
            (TestConstants.SecondUserId, TestConstants.SecondUserUsername, "second@example.com", UserRole.RegularUser, 15, null),
            (TestConstants.ModeratorUserId, TestConstants.ModeratorUserUsername, "moderator@example.com", UserRole.Moderator, 100, null),
            (TestConstants.MentorUserId, TestConstants.MentorUserUsername, "mentor@example.com", UserRole.Mentor, 200, null),
            (TestConstants.SeniorModeratorUserId, TestConstants.SeniorModeratorUserUsername, "seniormod@example.com", UserRole.SeniorModerator, 300, null),
            // Inactive users (subscribed to games/blogs but haven't been active for a while)
            (TestConstants.InactiveUser1Id, TestConstants.InactiveUser1Username, "inactive1@example.com", UserRole.RegularUser, 180, 90),
            (TestConstants.InactiveUser2Id, TestConstants.InactiveUser2Username, "inactive2@example.com", UserRole.RegularUser, 365, 120),
        };

        var allUsers = coreUsers.Concat(TestConstants.TestimonialUserIds.Select((id, i) => (
            Id: id,
            Username: TestConstants.TestimonialUsernames[i],
            Email: $"{TestConstants.TestimonialUsernames[i].ToLowerInvariant()}@example.com",
            Role: UserRole.RegularUser,
            DaysAgo: 50 + i * 30,
            InactiveDays: (int?)null
        )));

        db.Users.AddRange(allUsers.Select(u => new User
        {
            UserId = u.Id,
            Username = u.Username,
            Email = u.Email,
            PasswordHash = "fakehash",
            Salt = "fakesalt",
            PasswordHashVersion = 2,
            Role = u.Role,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-u.DaysAgo),
            LastActivityUtc = u.InactiveDays.HasValue
                ? DateTimeOffset.UtcNow.AddDays(-u.InactiveDays.Value)
                : DateTimeOffset.UtcNow,
            IsRemoved = false,
            Status = string.Empty,
            Name = string.Empty,
            Location = string.Empty,
            Info = string.Empty
        }));
    }

    private static void SeedBoards(DmDbContext db)
    {
        if (db.Set<Board>().Any(b => b.BoardId == TestConstants.TestBoardId)) return;

        db.Set<Board>().Add(new Board
        {
            BoardId = TestConstants.TestBoardId,
            Title = TestConstants.TestBoardTitle,
            Alias = "test",
            Description = "Test board for integration tests",
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.RegularUser,
            TopicsCount = 2,
            Order = 1
        });
    }

    private static void SeedTopics(DmDbContext db)
    {
        if (db.Set<Topic>().Any(t => t.TopicId == TestConstants.TestTopicId)) return;

        var topics = new (Guid Id, Guid AuthorId, string Title, string Text, int HoursAgo, int TopicNumber)[]
        {
            (TestConstants.TestTopicId, TestConstants.TestUserId, "Test Topic", "Test topic content for integration tests.", 24, 1),
            (TestConstants.SecondTopicId, TestConstants.SecondUserId, "Second Test Topic", "Second test topic owned by another user.", 12, 2),
        };

        db.Set<Topic>().AddRange(topics.Select(t => new Topic
        {
            TopicId = t.Id,
            BoardId = TestConstants.TestBoardId,
            AuthorId = t.AuthorId,
            Title = t.Title,
            Text = t.Text,
            TopicNumber = t.TopicNumber,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-t.HoursAgo),
            IsRemoved = false,
            IsAttached = false,
            IsClosed = false
        }));
    }

    private static void SeedGames(DmDbContext db)
    {
        if (db.Set<Game>().Any(g => g.GameId == TestConstants.TestGameId)) return;

        // (Id, AuthorId, Title, System, Setting, Info, DaysAgo, ActivatedDaysAgo, IsOpen, Count, Comments)
        var games = new (Guid Id, Guid AuthorId, string Title, string System, string Setting, string Info,
            int CreatedDaysAgo, int? ActivatedDaysAgo, bool IsOpen, int RecruitCount, CommentsAccessMode Comments)[]
        {
            (TestConstants.TestGameId, TestConstants.TestUserId,
                "Test Game", "D&D 5e", "Forgotten Realms", "Test game for integration tests.",
                10, null, true, 1, CommentsAccessMode.Public),
            (TestConstants.SecondGameId, TestConstants.SecondUserId,
                "Second Test Game", "Pathfinder", "Golarion", "Another test game owned by different user.",
                5, null, false, 1, CommentsAccessMode.Readonly),
            (TestConstants.InitialRecruitmentGameId, TestConstants.TestUserId,
                "Initial Recruitment Game", "GURPS", "Fantasy", "Game with first-time recruitment.",
                3, null, true, 1, CommentsAccessMode.Public),
            (TestConstants.SubsequentRecruitmentGameId, TestConstants.TestUserId,
                "Subsequent Recruitment Game", "Fate", "Urban Fantasy", "Game with subsequent recruitment.",
                30, 28, true, 2, CommentsAccessMode.Public),
            (TestConstants.ClosedRecruitmentGameId, TestConstants.SecondUserId,
                "Closed Recruitment Game", "World of Darkness", "Gothic Horror", "Game with closed recruitment.",
                60, 55, false, 2, CommentsAccessMode.Readonly),
            (TestConstants.NoRecruitmentGameId, TestConstants.AdminUserId,
                "Invitation Only Game", "Словеска", "Original", "Game that was always by invitation only.",
                90, 85, false, 0, CommentsAccessMode.Private),
        };

        var gameList = games.ToArray();
        db.Set<Game>().AddRange(gameList.Select((g, i) => new Game
        {
            GameId = g.Id,
            // SerialNumber is left to the identity sequence: writing it by hand leaves the
            // sequence pointing at a number the table already holds, so the next row the
            // application creates collides with a seeded one on the readable address.
            PublicId = $"game{(char)('a' + i)}",
            MasterId = g.AuthorId,
            Title = g.Title,
            SystemName = g.System,
            NarrativeSetting = g.Setting,
            Info = g.Info,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-g.CreatedDaysAgo),
            ActivatedUtc = g.ActivatedDaysAgo.HasValue ? DateTimeOffset.UtcNow.AddDays(-g.ActivatedDaysAgo.Value) : null,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            IsRecruitmentOpen = g.IsOpen,
            RecruitmentCount = g.RecruitCount,
            RecruitmentStartedUtc = g.IsOpen ? DateTimeOffset.UtcNow.AddDays(-Math.Min(5, g.CreatedDaysAgo)) : null,
            CommentsAccessMode = g.Comments,
            IsRemoved = false
        }));
    }

    private static void SeedGameTags(DmDbContext db)
    {
        if (db.Set<GameTag>().Any(t => t.GameId == TestConstants.TestGameId)) return;

        static Guid T(string hex) => Guid.Parse($"00000000-0000-0000-0000-0000000000{hex}");

        // GameId -> TagHexes
        var gameTagMap = new Dictionary<Guid, string[]>
        {
            [TestConstants.TestGameId] = ["03", "2b", "1b", "31", "2c", "35"],
            [TestConstants.SecondGameId] = ["0d", "2b", "1c", "31", "34", "35"],
            [TestConstants.InitialRecruitmentGameId] = ["0a", "2b", "2f", "3c", "36"],
            [TestConstants.SubsequentRecruitmentGameId] = ["08", "2b", "22", "23", "31", "33", "3e"],
            [TestConstants.ClosedRecruitmentGameId] = ["15", "29", "22", "27", "31", "34", "39", "35"],
            [TestConstants.NoRecruitmentGameId] = ["18", "2b", "2f", "3a", "3e"],
        };

        db.Set<GameTag>().AddRange(
            gameTagMap.SelectMany(kv => kv.Value.Select(hex => new GameTag
            {
                GameTagId = Guid.NewGuid(),
                GameId = kv.Key,
                TagId = T(hex)
            })));
    }

    private static void SeedBlogs(DmDbContext db)
    {
        if (db.Blogs.Any(b => b.BlogId == TestConstants.TestBlogId)) return;

        var blogs = new (Guid Id, Guid AuthorId, string Title, string Description, int DaysAgo)[]
        {
            (TestConstants.TestBlogId, TestConstants.TestUserId, "Test Blog", "Test blog for integration tests.", 5),
            (TestConstants.SecondBlogId, TestConstants.SecondUserId, "Second Test Blog", "Another test blog owned by different user.", 2),
        };

        db.Blogs.AddRange(blogs.Select((b, i) => new Blog
        {
            BlogId = b.Id,
            // SerialNumber is left to the identity sequence: writing it by hand leaves the
            // sequence pointing at a number the table already holds, so the next row the
            // application creates collides with a seeded one on the readable address.
            PublicId = $"blog{(char)('a' + i)}",
            AuthorId = b.AuthorId,
            Title = b.Title,
            Description = b.Description,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-b.DaysAgo),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsEnabled = true,
            IsRemoved = false
        }));
    }

    private static void SeedRooms(DmDbContext db)
    {
        if (db.Set<Room>().Any(r => r.RoomId == TestConstants.TestRoomId)) return;

        db.Set<Room>().Add(new Room
        {
            RoomId = TestConstants.TestRoomId,
            GameId = TestConstants.TestGameId,
            RoomNumber = 1,
            Title = "Main Room",
            AccessType = RoomAccessType.Open,
            Type = RoomType.Chat,
            OrderNumber = 1.0,
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
            IsRemoved = false
        });
    }

    /// <summary>
    /// Seed one post + one positive review in TestGame's main room so
    /// the <c>/v1/posts</c> rated-listing has at least one row to
    /// return. The rated endpoint projects <c>post.room.game</c> as a
    /// full sidebar-tier GameRef (master, assistants, activeCharacters,
    /// recruitment, subscribersCount), and integration tests assert
    /// the JSON shape — without this seed the endpoint would return
    /// an empty list and the assertions couldn't distinguish "mapping
    /// drops the field" from "no posts in DB".
    /// </summary>
    private static void SeedGamePosts(DmDbContext db)
    {
        if (db.Set<Post>().Any(p => p.PostId == TestConstants.TestGamePostId)) return;

        db.Set<Post>().Add(new Post
        {
            PostId = TestConstants.TestGamePostId,
            RoomId = TestConstants.TestRoomId,
            CharacterId = TestConstants.TestCharacterId,
            AuthorId = TestConstants.TestUserId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-12),
            GameText = "Test post body (in-character content)",
            MetagameText = null,
            PrivateAddresseeSnapshotJson = "{}",
            IsRemoved = false
        });

        db.Set<PostReview>().Add(new PostReview
        {
            PostReviewId = TestConstants.TestGamePostReviewId,
            AuthorId = TestConstants.SecondUserId,
            PostId = TestConstants.TestGamePostId,
            PostAuthorId = TestConstants.TestUserId,
            GameId = TestConstants.TestGameId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-6),
            Text = null,
            SignValue = 1, // Positive: makes the post appear in the rated listing
            IsRemoved = false
        });
    }

    /// <summary>
    /// Seed two active player characters into TestGame so tooltips
    /// exercising game.ActiveCharacters / room participants have real
    /// data to render. Without this, EnrichGamesAsync returns an empty
    /// list and tests can't distinguish "mapping drops the field"
    /// from "no characters in DB", which is exactly the ambiguity
    /// that let the original mapping bug go unnoticed.
    /// </summary>
    private static void SeedCharacters(DmDbContext db)
    {
        if (db.Set<Character>().Any(c => c.CharacterId == TestConstants.TestCharacterId)) return;

        db.Set<Character>().AddRange(
            new Character
            {
                CharacterId = TestConstants.TestCharacterId,
                GameId = TestConstants.TestGameId,
                AuthorId = TestConstants.TestUserId,
                Name = TestConstants.TestCharacterName,
                Status = CharacterStatus.Active,
                IsNpc = false,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-5),
                IsRemoved = false
            },
            new Character
            {
                CharacterId = TestConstants.SecondCharacterId,
                GameId = TestConstants.TestGameId,
                AuthorId = TestConstants.SecondUserId,
                Name = TestConstants.SecondCharacterName,
                Status = CharacterStatus.Active,
                IsNpc = false,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-4),
                IsRemoved = false
            });
    }

    private static void SeedChats(DmDbContext db)
    {
        if (db.Chats.Any(c => c.ChatId == TestConstants.TestChatId)) return;

        // The global chat is not seeded here: InitialCreate ships it, and the
        // tests must see the row the site actually starts with rather than a
        // convenient copy of it.

        // Direct chat between TestUser and SecondUser
        db.Chats.Add(new Chat
        {
            ChatId = TestConstants.TestChatId,
            // SerialNumber is left to the identity sequence: writing it by hand leaves the
            // sequence pointing at a number the table already holds, so the next row the
            // application creates collides with a seeded one on the readable address.
            PublicId = "chata",
            Type = ChatType.Direct,
            Title = string.Empty
        });

        db.UserChatLinks.AddRange(
            new[] { TestConstants.TestUserId, TestConstants.SecondUserId }
                .Select(userId => new UserChatLink
                {
                    UserChatLinkId = Guid.NewGuid(),
                    ChatId = TestConstants.TestChatId,
                    UserId = userId,
                    IsRemoved = false
                }));

        db.Messages.Add(new Message
        {
            MessageId = TestConstants.TestMessageId,
            ChatId = TestConstants.TestChatId,
            UserId = TestConstants.TestUserId,
            Text = "This is a test message.",
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-2),
            IsRemoved = false
        });

        // Global chat messages for testing
        var globalChatMessages = new (Guid UserId, string Text, int MinutesAgo)[]
        {
            (TestConstants.TestUserId, "Привет всем!", 120),
            (TestConstants.SecondUserId, "Привет! Кто-нибудь хочет поиграть в D&D?", 90),
            (TestConstants.ModeratorUserId, "Добро пожаловать в глобальный чат! Соблюдайте правила сообщества.", 60),
            (TestConstants.AdminUserId, "Напоминаем: завтра технические работы с 10:00 до 12:00.", 30),
            (TestConstants.TestUserId, "Спасибо за информацию!", 15),
        };

        db.Messages.AddRange(globalChatMessages.Select((m, i) => new Message
        {
            MessageId = Guid.NewGuid(),
            ChatId = Chat.GlobalChatId,
            UserId = m.UserId,
            Text = m.Text,
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-m.MinutesAgo),
            IsRemoved = false
        }));
    }

    private static void SeedBoardModerators(DmDbContext db)
    {
        if (db.Set<BoardModerator>().Any(m =>
                m.BoardId == TestConstants.TestBoardId && m.UserId == TestConstants.ModeratorUserId)) return;

        // Assign Moderator as the board moderator for Test Board
        db.Set<BoardModerator>().Add(new BoardModerator
        {
            BoardModeratorId = Guid.NewGuid(),
            BoardId = TestConstants.TestBoardId,
            UserId = TestConstants.ModeratorUserId
        });
    }

    private static void SeedSubscriptions(DmDbContext db)
    {
        if (db.Subscriptions.Any(s => s.SubscriberId == TestConstants.InactiveUser1Id)) return;

        // Inactive users subscribed to games and blogs
        var subscriptions = new (Guid SubscriberId, SubscriptionTargetType TargetType, Guid TargetId, int DaysAgo)[]
        {
            // InactiveUser1 subscribed to TestGame and TestBlog
            (TestConstants.InactiveUser1Id, SubscriptionTargetType.Game, TestConstants.TestGameId, 60),
            (TestConstants.InactiveUser1Id, SubscriptionTargetType.Blog, TestConstants.TestBlogId, 45),
            // InactiveUser2 subscribed to SecondGame and both blogs
            (TestConstants.InactiveUser2Id, SubscriptionTargetType.Game, TestConstants.SecondGameId, 100),
            (TestConstants.InactiveUser2Id, SubscriptionTargetType.Blog, TestConstants.TestBlogId, 80),
            (TestConstants.InactiveUser2Id, SubscriptionTargetType.Blog, TestConstants.SecondBlogId, 70),
        };

        db.Subscriptions.AddRange(subscriptions.Select(s => new Subscription
        {
            SubscriptionId = Guid.NewGuid(),
            SubscriberId = s.SubscriberId,
            TargetType = s.TargetType,
            TargetId = s.TargetId,
            Settings = s.TargetType == SubscriptionTargetType.Game
                ? SubscriptionSettings.GameReaderDefault
                : SubscriptionSettings.BlogReaderDefault,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-s.DaysAgo)
        }));
    }

    private static void SeedTestimonials(DmDbContext db)
    {
        if (db.WebsiteTestimonials.Any(t => t.WebsiteTestimonialId == TestConstants.TestTestimonialId)) return;

        // (TestimonialId, UserId, Text, DaysAgo)
        var testimonials = new (Guid TestimonialId, Guid UserId, string Text, int DaysAgo)[]
        {
            (TestConstants.TestTestimonialId, TestConstants.TestUserId,
                "Пришел сюда по рекомендации друга около трех лет назад, и с тех пор это стало моим основным хобби. Особенно нравится, что здесь можно найти игры на любой вкус — от классического фэнтези до киберпанка и постапокалипсиса. Мастера в большинстве своем отзывчивые и готовы помочь новичкам разобраться в правилах. Отдельный плюс — возможность играть в своем темпе, не подстраиваясь под чужое расписание. Конечно, бывают и неудачные игры, но это скорее исключение. В целом — отличное место для тех, кто любит писать и создавать истории вместе с другими людьми.", 120),
            (TestConstants.AdminTestimonialId, TestConstants.AdminUserId,
                "Лучшее место для текстовых ролевых игр!", 90),
            (TestConstants.SecondTestimonialId, TestConstants.SecondUserId,
                "Удобный интерфейс, дружелюбное комьюнити. Нашел здесь много интересных игр и познакомился с классными людьми.", 45),
            (TestConstants.ModeratorTestimonialId, TestConstants.ModeratorUserId,
                "Играю здесь с 2019 года. За это время площадка сильно изменилась в лучшую сторону — новый дизайн, удобные уведомления, быстрый поиск игр. Модерация работает оперативно, конфликты решаются справедливо. Единственное, чего не хватает — мобильного приложения, но и веб-версия на телефоне работает нормально. Рекомендую всем, кто хочет попробовать форумные ролевые игры.", 15),
            (TestConstants.MentorTestimonialId, TestConstants.MentorUserId,
                "Отличная площадка для новичков. Всегда рады помочь!", 60),
            (TestConstants.SeniorModeratorTestimonialId, TestConstants.SeniorModeratorUserId,
                "Начинал здесь как обычный игрок много лет назад. Тогда площадка была совсем другой — простенький форум, несколько десятков активных пользователей, пара сотен игр. Сейчас это полноценное сообщество с тысячами участников, продуманной системой тегов, удобным редактором постов и множеством других функций. Приятно осознавать, что внес свой вклад в развитие проекта. Здесь я нашел не только увлечение, но и настоящих друзей, с некоторыми из которых общаюсь уже вне площадки. Если вы любите писать, придумывать персонажей и погружаться в интересные сюжеты — вам точно сюда.", 180),
            (TestConstants.AdditionalTestimonialIds[0], TestConstants.TestimonialUserIds[0],
                "Зашел случайно, остался навсегда. Атмосфера здесь особенная — люди действительно любят то, чем занимаются.", 7),
            (TestConstants.AdditionalTestimonialIds[1], TestConstants.TestimonialUserIds[1],
                "Долго искал площадку для словесок, и DM.am оказался именно тем, что нужно. Простой интерфейс, понятные правила, живое сообщество. Особенно радует, что можно найти игры практически в любом жанре — от классического фэнтези до современных детективов. Мастера отзывчивые, всегда готовы объяснить непонятные моменты. Единственный минус — иногда сложно выбрать, в какую игру вступить, потому что интересных слишком много!", 25),
            (TestConstants.AdditionalTestimonialIds[2], TestConstants.TestimonialUserIds[2],
                "Отличное место для творческих людей. Рекомендую!", 35),
            (TestConstants.AdditionalTestimonialIds[3], TestConstants.TestimonialUserIds[3],
                "Площадка, которая объединяет людей с похожими интересами. Здесь я нашла не просто игры, а настоящее хобби, которое помогает отвлечься от рутины и погрузиться в увлекательные истории. Нравится, что каждый может найти что-то свое — есть игры с акцентом на боевку, есть чисто отыгрышевые, есть смешанные. Модерация адекватная, правила понятные.", 52),
            (TestConstants.AdditionalTestimonialIds[4], TestConstants.TestimonialUserIds[4],
                "Супер! Всем советую!", 70),
            (TestConstants.AdditionalTestimonialIds[5], TestConstants.TestimonialUserIds[5],
                "Пишу здесь уже пять лет. За это время сменилось несколько версий сайта, но атмосфера осталась прежней — дружелюбной и творческой. Особенно ценю возможность участвовать в нескольких играх одновременно и писать в удобное время. Форумный формат идеально подходит для тех, кто любит продумывать каждую реплику своего персонажа.", 85),
            (TestConstants.AdditionalTestimonialIds[6], TestConstants.TestimonialUserIds[6],
                "Лучшая площадка для форумных ролевых игр в рунете. Без преувеличения.", 110),
            (TestConstants.AdditionalTestimonialIds[7], TestConstants.TestimonialUserIds[7],
                "Классное комьюнити, интересные игры, удобный сайт. Что еще нужно?", 140),
            (TestConstants.AdditionalTestimonialIds[8], TestConstants.TestimonialUserIds[8],
                "Пришла сюда по рекомендации подруги и не пожалела. Первые дни было немного страшно — все-таки новое место, незнакомые люди. Но оказалось, что здесь очень приветливо относятся к новичкам. Помогли разобраться с правилами, подсказали хорошие игры для старта. Сейчас у меня уже три активных персонажа в разных играх, и я планирую создать свою собственную игру в ближайшее время.", 165),
            (TestConstants.AdditionalTestimonialIds[9], TestConstants.TestimonialUserIds[9],
                "Годы идут, а DM.am остается любимым местом для творчества.", 200),
            (TestConstants.AdditionalTestimonialIds[10], TestConstants.TestimonialUserIds[10],
                "Хороший сайт для тех, кто любит писать.", 230),
            (TestConstants.AdditionalTestimonialIds[11], TestConstants.TestimonialUserIds[11],
                "Не ожидал, что форумные игры могут быть настолько затягивающими! Начал полгода назад с небольшого персонажа в фэнтези-игре, а теперь уже сам вожу две игры и участвую еще в трех. Формат постовой игры отлично подходит для работающих людей — можно писать когда удобно, не нужно подстраиваться под чужое расписание. Сообщество отзывчивое, всегда можно найти партнеров для игры или просто пообщаться на форуме.", 260),
            (TestConstants.AdditionalTestimonialIds[12], TestConstants.TestimonialUserIds[12],
                "10/10, рекомендую всем любителям ролевых игр!", 300),
            (TestConstants.AdditionalTestimonialIds[13], TestConstants.TestimonialUserIds[13],
                "Площадка, на которой хочется оставаться. Здесь собрались действительно увлеченные люди, которые создают потрясающие истории. Каждая игра — это маленький мир со своими правилами и атмосферой. Очень нравится система тегов, которая помогает быстро найти игру по интересам. Отдельное спасибо разработчикам за удобный редактор постов с поддержкой форматирования.", 350),
            (TestConstants.AdditionalTestimonialIds[14], TestConstants.TestimonialUserIds[14],
                "DM — это как таверна на перекрестке миров. Заходишь случайно, а остаешься навсегда.", 5),
            (TestConstants.AdditionalTestimonialIds[15], TestConstants.TestimonialUserIds[15],
                "Когда создаешь очередного персонажа, невольно вкладываешь в него частицу себя. И это прекрасно — здесь можно быть кем угодно, исследовать грани своей личности через призму выдуманных историй. За годы на площадке я создала десятки персонажей, и каждый из них научил меня чему-то новому о себе.", 42),
            (TestConstants.AdditionalTestimonialIds[16], TestConstants.TestimonialUserIds[16],
                "DM — это Сила и Мощь!", 380),
            (TestConstants.AdditionalTestimonialIds[17], TestConstants.TestimonialUserIds[17],
                "Писать здесь — как дышать. Без этого уже не представляю свою жизнь. Форумные игры дают то, чего не дают книги и фильмы — ты не просто наблюдатель, а активный участник истории. Каждый твой выбор влияет на сюжет, каждое слово имеет вес. И когда история получается по-настоящему захватывающей — это непередаваемое чувство.", 18),
            (TestConstants.AdditionalTestimonialIds[18], TestConstants.TestimonialUserIds[18],
                "Место, где рождаются легенды.", 95),
            (TestConstants.AdditionalTestimonialIds[19], TestConstants.TestimonialUserIds[19],
                "Раньше считал, что форумные игры — это что-то несерьезное. Попробовал ради интереса и понял, как сильно ошибался. Здесь собрались талантливые авторы, которые создают тексты уровня профессиональных писателей. Некоторые игровые посты читаются как главы хорошего романа. Спасибо всем, кто делает эту площадку такой особенной.", 155),
            (TestConstants.AdditionalTestimonialIds[20], TestConstants.TestimonialUserIds[20],
                "Идеальное место для творческих людей. Если вы любите писать — вам точно сюда.", 270),
            (TestConstants.AdditionalTestimonialIds[21], TestConstants.TestimonialUserIds[21],
                "Пять звезд из пяти! Без вариантов.", 320),
            (TestConstants.AdditionalTestimonialIds[22], TestConstants.TestimonialUserIds[22],
                "История о том, как я искал площадку для словесных ролевых игр, могла бы занять отдельный пост. Перепробовал все — форумы, дискорд-серверы, специализированные сайты. И только здесь нашел то, что искал: удобный интерфейс, активное сообщество, разнообразие жанров и адекватную модерацию. Уже два года здесь, и уходить не планирую.", 78),
            (TestConstants.AdditionalTestimonialIds[23], TestConstants.TestimonialUserIds[23],
                "Здесь каждый найдет что-то свое.", 400),
            (TestConstants.AdditionalTestimonialIds[24], TestConstants.TestimonialUserIds[24],
                "Лучшая площадка для форумных ролевых игр. Проверено годами.", 450),
            (TestConstants.AdditionalTestimonialIds[25], TestConstants.TestimonialUserIds[25],
                "Сначала пришла просто посмотреть, что это такое. Потом создала пробного персонажа. Потом еще одного. И еще. Сейчас у меня восемь активных персонажей в разных играх, и я веду свою собственную игру по авторскому миру. Эта площадка захватывает — в хорошем смысле. Здесь можно реализовать любые творческие идеи и найти единомышленников.", 130),
            (TestConstants.AdditionalTestimonialIds[26], TestConstants.TestimonialUserIds[26],
                "Годный сайт, рекомендую!", 500),
            (TestConstants.AdditionalTestimonialIds[27], TestConstants.TestimonialUserIds[27],
                "DM научил меня писать. Серьезно. Когда я только пришел сюда, мои посты были короткими и корявыми. Но здесь окружение такое, что хочется стараться, хочется писать лучше. Читаешь посты других игроков и вдохновляешься. Сейчас, спустя несколько лет, я пишу совсем на другом уровне — и это заслуга не только моя, но и этого сообщества.", 210),
        };

        db.WebsiteTestimonials.AddRange(testimonials.Select(t => new WebsiteTestimonial
        {
            WebsiteTestimonialId = t.TestimonialId,
            AuthorId = t.UserId,
            Text = t.Text,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-t.DaysAgo),
            IsRemoved = false
        }));
    }

    #endregion
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
