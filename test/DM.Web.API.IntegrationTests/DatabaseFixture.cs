using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.BusinessObjects.Games;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.Core.Dto.Enums;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Shared fixture that manages the PostgreSQL container lifecycle.
/// This fixture is shared across all test classes in the collection
/// to avoid spinning up multiple containers.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private CustomWebApplicationFactory? _sharedFactory;

    /// <summary>
    /// Connection string for the PostgreSQL container
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

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
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("dm3_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    }

    /// <summary>
    /// Initialize the container and seed the database
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Create schema (note: schema has complex FK constraints that may need adjustment)
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);
        IsSeeded = true;
    }

    /// <summary>
    /// Stop the container and dispose factory
    /// </summary>
    public async Task DisposeAsync()
    {
        _sharedFactory?.Dispose();
        await _container.DisposeAsync();
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
        // FK constraints on EntityId to BOTH Games AND ForumTopics tables,
        // which cannot both be satisfied simultaneously.
        // SeedComments(db);
        // await db.SaveChangesAsync();

        SeedRooms(db);
        await db.SaveChangesAsync();

        SeedConversations(db);
        await db.SaveChangesAsync();
    }

    private static void SeedUsers(DmDbContext db)
    {
        // Test user
        if (!db.Users.Any(u => u.Login == TestConstants.TestUserLogin))
        {
            db.Users.Add(new User
            {
                UserId = TestConstants.TestUserId,
                Login = TestConstants.TestUserLogin,
                Email = "test@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Activated = true,
                Role = UserRole.RegularUser,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-30),
                LastActivityUtc = DateTimeOffset.UtcNow,
                IsRemoved = false,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty,
                Icq = string.Empty,
                Skype = string.Empty,
                ProfilePictureUrl = string.Empty,
                SmallProfilePictureUrl = string.Empty,
                MediumProfilePictureUrl = string.Empty,
#pragma warning disable CS0618 // TimezoneId is obsolete
                TimezoneId = "UTC"
#pragma warning restore CS0618
            });
        }

        // Admin user
        if (!db.Users.Any(u => u.Login == TestConstants.AdminUserLogin))
        {
            db.Users.Add(new User
            {
                UserId = TestConstants.AdminUserId,
                Login = TestConstants.AdminUserLogin,
                Email = "admin@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Activated = true,
                Role = UserRole.Admin,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-365),
                LastActivityUtc = DateTimeOffset.UtcNow,
                IsRemoved = false,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty,
                Icq = string.Empty,
                Skype = string.Empty,
                ProfilePictureUrl = string.Empty,
                SmallProfilePictureUrl = string.Empty,
                MediumProfilePictureUrl = string.Empty,
#pragma warning disable CS0618 // TimezoneId is obsolete
                TimezoneId = "UTC"
#pragma warning restore CS0618
            });
        }

        // Second user (for multi-user scenarios)
        if (!db.Users.Any(u => u.Login == TestConstants.SecondUserLogin))
        {
            db.Users.Add(new User
            {
                UserId = TestConstants.SecondUserId,
                Login = TestConstants.SecondUserLogin,
                Email = "second@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Activated = true,
                Role = UserRole.RegularUser,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-15),
                LastActivityUtc = DateTimeOffset.UtcNow,
                IsRemoved = false,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty,
                Icq = string.Empty,
                Skype = string.Empty,
                ProfilePictureUrl = string.Empty,
                SmallProfilePictureUrl = string.Empty,
                MediumProfilePictureUrl = string.Empty,
#pragma warning disable CS0618 // TimezoneId is obsolete
                TimezoneId = "UTC"
#pragma warning restore CS0618
            });
        }

        // Moderator user
        if (!db.Users.Any(u => u.Login == TestConstants.ModeratorUserLogin))
        {
            db.Users.Add(new User
            {
                UserId = TestConstants.ModeratorUserId,
                Login = TestConstants.ModeratorUserLogin,
                Email = "moderator@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Activated = true,
                Role = UserRole.Moderator,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-100),
                LastActivityUtc = DateTimeOffset.UtcNow,
                IsRemoved = false,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty,
                Icq = string.Empty,
                Skype = string.Empty,
                ProfilePictureUrl = string.Empty,
                SmallProfilePictureUrl = string.Empty,
                MediumProfilePictureUrl = string.Empty,
#pragma warning disable CS0618 // TimezoneId is obsolete
                TimezoneId = "UTC"
#pragma warning restore CS0618
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
                CreateTopicPolicy = BoardAccessPolicy.Player,
                TopicsCount = 1,
                Order = 1
            });
        }
    }

    private static void SeedTopics(DmDbContext db)
    {
        if (!db.Set<ForumTopic>().Any())
        {
            db.Set<ForumTopic>().Add(new ForumTopic
            {
                ForumTopicId = TestConstants.TestTopicId,
                BoardId = TestConstants.TestBoardId,
                UserId = TestConstants.TestUserId,
                Title = "Test Topic",
                Text = "This is a test topic content for integration tests.",
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                IsRemoved = false,
                IsAttached = false,
                IsClosed = false
            });

            db.Set<ForumTopic>().Add(new ForumTopic
            {
                ForumTopicId = TestConstants.SecondTopicId,
                BoardId = TestConstants.TestBoardId,
                UserId = TestConstants.SecondUserId,
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
        if (!db.Set<DM.Services.DataAccess.BusinessObjects.Common.Comment>().Any())
        {
            // Comment for game (FK_Comments_Games_EntityId requires this)
            db.Set<DM.Services.DataAccess.BusinessObjects.Common.Comment>().Add(
                new DM.Services.DataAccess.BusinessObjects.Common.Comment
                {
                    CommentId = TestConstants.TestCommentId,
                    EntityId = TestConstants.TestGameId,
                    UserId = TestConstants.TestUserId,
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
                MasterId = TestConstants.TestUserId,
                Title = "Test Game",
                SystemName = "D&D 5e",
                NarrativeSetting = "Forgotten Realms",
                Info = "This is a test game for integration tests.",
                Notepad = string.Empty,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-10),
                Status = GameStatus.Active,
                PremoderationStatus = PremoderationStatus.Approved,
                IsRecruitmentOpen = true,
                CommentariesAccessMode = CommentariesAccessMode.Public,
                IsRemoved = false
            });

            db.Set<Game>().Add(new Game
            {
                GameId = TestConstants.SecondGameId,
                MasterId = TestConstants.SecondUserId,
                Title = "Second Test Game",
                SystemName = "Pathfinder",
                NarrativeSetting = "Golarion",
                Info = "This is another test game owned by a different user.",
                Notepad = string.Empty,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-5),
                Status = GameStatus.Active,
                PremoderationStatus = PremoderationStatus.Approved,
                IsRecruitmentOpen = false,
                CommentariesAccessMode = CommentariesAccessMode.Readonly,
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

    private static void SeedConversations(DmDbContext db)
    {
        if (!db.Set<Conversation>().Any())
        {
            var conversation = new Conversation
            {
                ConversationId = TestConstants.TestConversationId,
                Visavi = true,
                Title = string.Empty
            };
            db.Set<Conversation>().Add(conversation);

            db.Set<UserConversationLink>().Add(new UserConversationLink
            {
                UserConversationLinkId = Guid.NewGuid(),
                ConversationId = TestConstants.TestConversationId,
                UserId = TestConstants.TestUserId,
                IsRemoved = false
            });

            db.Set<UserConversationLink>().Add(new UserConversationLink
            {
                UserConversationLinkId = Guid.NewGuid(),
                ConversationId = TestConstants.TestConversationId,
                UserId = TestConstants.SecondUserId,
                IsRemoved = false
            });

            db.Set<Message>().Add(new Message
            {
                MessageId = TestConstants.TestMessageId,
                ConversationId = TestConstants.TestConversationId,
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
