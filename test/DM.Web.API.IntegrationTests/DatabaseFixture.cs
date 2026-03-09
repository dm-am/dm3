using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Shared fixture that manages the PostgreSQL container lifecycle.
/// This fixture is shared across all test classes in the collection
/// to avoid spinning up multiple containers.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly MongoDbContainer _mongoContainer;
    private readonly RabbitMqContainer _rabbitMqContainer;
    private CustomWebApplicationFactory? _sharedFactory;

    /// <summary>
    /// Connection string for the PostgreSQL container
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Connection string for the MongoDB container
    /// </summary>
    public string MongoConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Connection string for the RabbitMQ container
    /// </summary>
    public string RabbitMqConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Indicates whether the database has been seeded
    /// </summary>
    public bool IsSeeded { get; private set; }

    /// <summary>
    /// Shared WebApplicationFactory for all tests (to avoid resource exhaustion)
    /// </summary>
    public CustomWebApplicationFactory Factory
    {
        get
        {
            _sharedFactory ??= new CustomWebApplicationFactory(this);
            return _sharedFactory;
        }
    }

    public DatabaseFixture()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("dm3_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7")
            .Build();

        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
    }

    /// <summary>
    /// Initialize containers and seed the database
    /// </summary>
    public async Task InitializeAsync()
    {
        // Start all containers in parallel
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _mongoContainer.StartAsync(),
            _rabbitMqContainer.StartAsync());

        ConnectionString = _postgresContainer.GetConnectionString();

        // Insert database name into connection string, keeping authSource=admin
        // Testcontainers returns: mongodb://user:pass@host:port/?directConnection=true
        // We need:               mongodb://user:pass@host:port/dm3_test?authSource=admin&directConnection=true
        var rawMongoUrl = _mongoContainer.GetConnectionString();
        MongoConnectionString = rawMongoUrl.Contains("/?")
            ? rawMongoUrl.Replace("/?", "/dm3_test?authSource=admin&")
            : rawMongoUrl.TrimEnd('/') + "/dm3_test";

        // RabbitMQ connection string
        RabbitMqConnectionString = _rabbitMqContainer.GetConnectionString();

        // Create schema (note: schema has complex FK constraints that may need adjustment)
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);
        IsSeeded = true;
    }

    /// <summary>
    /// Stop containers and dispose factory
    /// </summary>
    public async Task DisposeAsync()
    {
        _sharedFactory?.Dispose();
        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _mongoContainer.DisposeAsync().AsTask(),
            _rabbitMqContainer.DisposeAsync().AsTask());
    }

    /// <summary>
    /// Create a new DbContext instance for the test database
    /// </summary>
    public DmDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new DmDbContext(options);
    }

    /// <summary>
    /// Seed test data into the database
    /// </summary>
    private static async Task SeedTestDataAsync(DmDbContext db)
    {
        // Seed in order to satisfy foreign key constraints
        SeedUsers(db);
        await db.SaveChangesAsync();

        SeedBoards(db);
        await db.SaveChangesAsync();

        SeedTopics(db);
        await db.SaveChangesAsync();

        SeedGames(db);
        await db.SaveChangesAsync();

        // Note: Comment seeding is disabled because the database schema has
        // FK constraints on EntityId to BOTH Games AND Topics tables,
        // which cannot both be satisfied simultaneously.
        // SeedComments(db);
        // await db.SaveChangesAsync();

        SeedRooms(db);
        await db.SaveChangesAsync();

        SeedChats(db);
        await db.SaveChangesAsync();
    }

    private static void SeedUsers(DmDbContext db)
    {
        // Test user
        if (!db.Users.Any(u => u.Username == TestConstants.TestUserUsername))
        {
            db.Users.Add(new User
            {
                UserId = TestConstants.TestUserId,
                Username = TestConstants.TestUserUsername,
                Email = "test@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Role = UserRole.RegularUser,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-30),
                LastActivityUtc = DateTimeOffset.UtcNow,
                IsRemoved = false,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty
            });
        }

        // Admin user
        if (!db.Users.Any(u => u.Username == TestConstants.AdminUserUsername))
        {
            db.Users.Add(new User
            {
                UserId = TestConstants.AdminUserId,
                Username = TestConstants.AdminUserUsername,
                Email = "admin@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Role = UserRole.Admin,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-365),
                LastActivityUtc = DateTimeOffset.UtcNow,
                IsRemoved = false,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty
            });
        }

        // Second user (for multi-user scenarios)
        if (!db.Users.Any(u => u.Username == TestConstants.SecondUserUsername))
        {
            db.Users.Add(new User
            {
                UserId = TestConstants.SecondUserId,
                Username = TestConstants.SecondUserUsername,
                Email = "second@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Role = UserRole.RegularUser,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-15),
                LastActivityUtc = DateTimeOffset.UtcNow,
                IsRemoved = false,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty
            });
        }

        // Moderator user
        if (!db.Users.Any(u => u.Username == TestConstants.ModeratorUserUsername))
        {
            db.Users.Add(new User
            {
                UserId = TestConstants.ModeratorUserId,
                Username = TestConstants.ModeratorUserUsername,
                Email = "moderator@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Role = UserRole.Moderator,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-100),
                LastActivityUtc = DateTimeOffset.UtcNow,
                IsRemoved = false,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty
            });
        }
    }

    private static void SeedBoards(DmDbContext db)
    {
        if (!db.Set<Board>().Any())
        {
            db.Set<Board>().Add(new Board
            {
                BoardId = TestConstants.TestBoardId,
                Title = "Test Board",
                Description = "Test board for integration tests",
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 1,
                Order = 1
            });
        }
    }

    private static void SeedTopics(DmDbContext db)
    {
        if (!db.Set<Topic>().Any())
        {
            db.Set<Topic>().Add(new Topic
            {
                TopicId = TestConstants.TestTopicId,
                BoardId = TestConstants.TestBoardId,
                AuthorId = TestConstants.TestUserId,
                Title = "Test Topic",
                Text = "This is a test topic content for integration tests.",
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                IsRemoved = false,
                IsAttached = false,
                IsClosed = false
            });

            db.Set<Topic>().Add(new Topic
            {
                TopicId = TestConstants.SecondTopicId,
                BoardId = TestConstants.TestBoardId,
                AuthorId = TestConstants.SecondUserId,
                Title = "Second Test Topic",
                Text = "This is a second test topic owned by another user.",
                CreatedUtc = DateTimeOffset.UtcNow.AddHours(-12),
                IsRemoved = false,
                IsAttached = false,
                IsClosed = false
            });
        }
    }

    private static void SeedComments(DmDbContext db)
    {
        if (!db.Set<DM.Infrastructure.Persistence.Entities.CrossDomain.Comment>().Any())
        {
            // Comment for game (FK_Comments_Games_EntityId requires this)
            db.Set<DM.Infrastructure.Persistence.Entities.CrossDomain.Comment>().Add(
                new DM.Infrastructure.Persistence.Entities.CrossDomain.Comment
                {
                    CommentId = TestConstants.TestCommentId,
                    EntityId = TestConstants.TestGameId,
                    AuthorId = TestConstants.TestUserId,
                    Text = "This is a test comment.",
                    CreatedUtc = DateTimeOffset.UtcNow.AddHours(-6),
                    IsRemoved = false
                });
        }
    }

    private static void SeedGames(DmDbContext db)
    {
        if (!db.Set<Game>().Any())
        {
            db.Set<Game>().Add(new Game
            {
                GameId = TestConstants.TestGameId,
                AuthorId = TestConstants.TestUserId,
                Title = "Test Game",
                SystemName = "D&D 5e",
                NarrativeSetting = "Forgotten Realms",
                Info = "This is a test game for integration tests.",
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-10),
                Status = ModuleStatus.Active,
                PremoderationStatus = PremoderationStatus.Approved,
                IsRecruitmentOpen = true,
                CommentsAccessMode = CommentsAccessMode.Public,
                IsRemoved = false
            });

            db.Set<Game>().Add(new Game
            {
                GameId = TestConstants.SecondGameId,
                AuthorId = TestConstants.SecondUserId,
                Title = "Second Test Game",
                SystemName = "Pathfinder",
                NarrativeSetting = "Golarion",
                Info = "This is another test game owned by a different user.",
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-5),
                Status = ModuleStatus.Active,
                PremoderationStatus = PremoderationStatus.Approved,
                IsRecruitmentOpen = false,
                CommentsAccessMode = CommentsAccessMode.Readonly,
                IsRemoved = false
            });
        }
    }

    private static void SeedRooms(DmDbContext db)
    {
        if (!db.Set<Room>().Any())
        {
            db.Set<Room>().Add(new Room
            {
                RoomId = TestConstants.TestRoomId,
                GameId = TestConstants.TestGameId,
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
    }

    private static void SeedChats(DmDbContext db)
    {
        if (!db.Chats.Any())
        {
            var chat = new Chat
            {
                ChatId = TestConstants.TestChatId,
                Type = DM.Domain.Core.Enums.ChatType.Direct,
                Title = string.Empty
            };
            db.Chats.Add(chat);

            db.UserChatLinks.Add(new UserChatLink
            {
                UserChatLinkId = Guid.NewGuid(),
                ChatId = TestConstants.TestChatId,
                UserId = TestConstants.TestUserId,
                IsRemoved = false
            });

            db.UserChatLinks.Add(new UserChatLink
            {
                UserChatLinkId = Guid.NewGuid(),
                ChatId = TestConstants.TestChatId,
                UserId = TestConstants.SecondUserId,
                IsRemoved = false
            });

            db.Messages.Add(new Message
            {
                MessageId = TestConstants.TestMessageId,
                ChatId = TestConstants.TestChatId,
                UserId = TestConstants.TestUserId,
                Text = "This is a test message.",
                CreatedUtc = DateTimeOffset.UtcNow.AddHours(-2),
                IsRemoved = false
            });
        }
    }
}

/// <summary>
/// Collection definition for tests that share the database fixture
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
