using DM.Domain.Core.Chats;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace DM.Web.API.IntegrationTests;

public partial class DatabaseFixture : IAsyncLifetime
{
    // The stand's image and locale (postgres:16, en_US.utf8): Alpine has no locales and orders names by byte.
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithEnvironment("POSTGRES_INITDB_ARGS", "--locale=en_US.utf8")
        .WithDatabase("dm3_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    // The broker container's user, handed to the host as a setting: the endpoint's userinfo is not
    // read.
    public const string BrokerUsername = "guest";

    public const string BrokerPassword = "guest";

    // The AMQP port is pinned: Docker reassigns a random port on restart, which the broker-restart tests do.
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.3-management-alpine")
        .WithUsername(BrokerUsername)
        .WithPassword(BrokerPassword)
        .WithPortBinding(45672, 5672)
        .Build();

    private CustomWebApplicationFactory? _sharedFactory;

    public string ConnectionString { get; private set; } = string.Empty;
    public string RabbitMqConnectionString { get; private set; } = string.Empty;

    public CustomWebApplicationFactory Factory => _sharedFactory ??= new CustomWebApplicationFactory(this);

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _rabbitMqContainer.StartAsync());

        ConnectionString = _postgresContainer.GetConnectionString();
        RabbitMqConnectionString = _rabbitMqContainer.GetConnectionString();

        await using var context = CreateDbContext();
        // The migration builds the schema and its seed rows, the database the site starts from.
        await context.Database.MigrateAsync();
        await SeedAllAsync(context);
    }

    public async ValueTask DisposeAsync()
    {
        _sharedFactory?.Dispose();
        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _rabbitMqContainer.DisposeAsync().AsTask());
    }

    public DmDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseNpgsql(ConnectionString)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors()
        .Options);

    // Stops the broker container without removing it: queues and persisted messages survive.
    public Task StopBrokerAsync() => _rabbitMqContainer.StopAsync();

    public Task StartBrokerAsync() => _rabbitMqContainer.StartAsync();

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

    // Every guard keys on a row this fixture owns: InitialCreate ships rows of its own.
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
            (TestConstants.TestUserId, TestConstants.TestUserLogin, "test@example.com", UserRole.RegularUser, 30, null),
            (TestConstants.AdminUserId, TestConstants.AdminUserLogin, "admin@example.com", UserRole.Admin, 365, null),
            (TestConstants.SecondUserId, TestConstants.SecondUserLogin, "second@example.com", UserRole.RegularUser, 15, null),
            (TestConstants.ModeratorUserId, TestConstants.ModeratorUserLogin, "moderator@example.com", UserRole.Moderator, 100, null),
            (TestConstants.MentorUserId, TestConstants.MentorUserLogin, "mentor@example.com", UserRole.Mentor, 200, null),
            (TestConstants.SeniorModeratorUserId, TestConstants.SeniorModeratorUserLogin, "seniormod@example.com", UserRole.SeniorModerator, 300, null),
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
            // SerialNumber is left to the identity sequence: one written by hand collides with the next row on the readable address.
            PublicId = $"blog{(char)('a' + i)}",
            AuthorId = b.AuthorId,
            Title = b.Title,
            Description = b.Description,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-b.DaysAgo),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            IsRemoved = false
        }));
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
            ChatId = WellKnownChats.GlobalChatId,
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

}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
