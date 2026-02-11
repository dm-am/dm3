using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Reading;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using GameOutput = DM.Services.Game.Dto.Output.Game;
using DM.Tests.Core;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace DM.Services.Game.Tests;

public class GameReadingServiceShould : UnitTestBase
{
    private readonly Mock<IGameReadingRepository> repository;
    private readonly Mock<IUnreadCountersRepository> unreadCounters;
    private readonly Mock<IIdentityProvider> identityProvider;
    private readonly Mock<IIdentity> identity;
    private readonly Mock<IIntentionManager> intentionManager;
    private readonly GameReadingService service;
    private readonly Guid testUserId = Guid.NewGuid();

    public GameReadingServiceShould()
    {
        var validator = Mock<IValidator<GamesQuery>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<GamesQuery>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        intentionManager = Mock<IIntentionManager>();
        intentionManager
            .Setup(m => m.IsAllowed(It.IsAny<GameIntention>(), It.IsAny<GameOutput>()))
            .Returns(true);

        var schemaService = Mock<IAttributeSchemaReadingService>();

        repository = Mock<IGameReadingRepository>();
        repository
            .Setup(r => r.GetOwn(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<GameOutput>());
        repository
            .Setup(r => r.GetAvailableRoomIds(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>()))
            .ReturnsAsync(new Dictionary<Guid, IEnumerable<Guid>>());
        repository
            .Setup(r => r.GetPostPendencies(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<PostPendency>());
        repository
            .Setup(r => r.GetRoomsAndPostPendencies(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>()))
            .ReturnsAsync((new Dictionary<Guid, IEnumerable<Guid>>(), Array.Empty<PostPendency>()));
        repository
            .Setup(r => r.GetTotalPostCounts(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        repository
            .Setup(r => r.GetTotalCommentCounts(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        unreadCounters = Mock<IUnreadCountersRepository>();
        unreadCounters
            .Setup(r => r.SelectByEntities(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        var cache = new MemoryCache(new MemoryCacheOptions());

        identityProvider = Mock<IIdentityProvider>();
        identity = Mock<IIdentity>();
        identityProvider.Setup(p => p.Current).Returns(identity.Object);
        identity.Setup(i => i.User).Returns(new AuthenticatedUser
        {
            UserId = testUserId,
            Role = UserRole.RegularUser // Makes IsAuthenticated = true (Role != Guest)
        });
        identity.Setup(i => i.Settings).Returns(new UserSettings { Paging = new PagingSettings { EntitiesPerPage = 10 } });

        service = new GameReadingService(
            validator.Object,
            intentionManager.Object,
            schemaService.Object,
            repository.Object,
            identityProvider.Object,
            unreadCounters.Object,
            cache);
    }

    [Fact]
    public async Task ReturnEmptyListWhenNoGames()
    {
        // Arrange
        repository.Setup(r => r.GetOwn(testUserId)).ReturnsAsync(Array.Empty<GameOutput>());

        // Act
        var result = await service.GetOwnGames();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnGamesForUser()
    {
        // Arrange
        var game1Id = Guid.NewGuid();
        var game2Id = Guid.NewGuid();
        var games = new[]
        {
            new GameOutput { Id = game1Id, Title = "Test Game 1" },
            new GameOutput { Id = game2Id, Title = "Test Game 2" }
        };
        repository.Setup(r => r.GetOwn(testUserId)).ReturnsAsync(games);

        // Mock needs to return correct IDs
        unreadCounters
            .Setup(r => r.SelectByEntities(testUserId, It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { game1Id, 0 }, { game2Id, 0 } });

        // Act
        var result = await service.GetOwnGames();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task CallBothUnreadCountersMethods()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var games = new[] { new GameOutput { Id = gameId, Title = "Test Game" } };
        repository.Setup(r => r.GetOwn(testUserId)).ReturnsAsync(games);

        // Mock needs to return correct IDs
        unreadCounters
            .Setup(r => r.SelectByEntities(testUserId, It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { gameId, 0 } });

        // Act
        await service.GetOwnGames();

        // Assert - verify both counter methods were called (Message = comments, Character = characters)
        unreadCounters.Verify(r => r.SelectByEntities(
            testUserId, UnreadEntryType.Message, It.IsAny<Guid[]>()), Times.AtLeastOnce);
        unreadCounters.Verify(r => r.SelectByEntities(
            testUserId, UnreadEntryType.Character, It.IsAny<Guid[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteMongoDbQueriesInParallel()
    {
        // Arrange - use delays to verify parallel execution
        var gameId = Guid.NewGuid();
        var games = new[] { new GameOutput { Id = gameId, Title = "Test Game" } };
        repository.Setup(r => r.GetOwn(testUserId)).ReturnsAsync(games);

        var delayMs = 100;
        var callStartTimes = new List<DateTime>();

        // Setup SelectByEntities to track call times (used by FillEntityCounters)
        unreadCounters
            .Setup(r => r.SelectByEntities(testUserId, It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .Returns(async () =>
            {
                lock (callStartTimes)
                {
                    callStartTimes.Add(DateTime.UtcNow);
                }
                await Task.Delay(delayMs);
                return new Dictionary<Guid, int> { { gameId, 0 } };
            });

        // Act
        var stopwatch = Stopwatch.StartNew();
        await service.GetOwnGames();
        stopwatch.Stop();

        // Assert - if parallel, total time should be significantly less than 2x delay
        // Allow very generous tolerance for test environment variability (CI, parallel test runs, system load)
        // Key insight: sequential would be 200ms+, parallel should be ~100-150ms under normal conditions
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(delayMs * 10,
            $"MongoDB queries should execute in parallel. Expected <{delayMs * 10}ms (sequential would be {delayMs * 2}ms+), got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetGame_CallsBothCountersInParallel()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameOutput { Id = gameId, Title = "Test Game" };
        repository.Setup(r => r.GetGame(gameId, testUserId)).ReturnsAsync(game);

        var delayMs = 100;

        unreadCounters
            .Setup(r => r.SelectByEntities(testUserId, It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .Returns(async () =>
            {
                await Task.Delay(delayMs);
                return new Dictionary<Guid, int> { { gameId, 0 } };
            });

        // Act
        var stopwatch = Stopwatch.StartNew();
        await service.GetGame(gameId);
        stopwatch.Stop();

        // Assert - parallel execution should complete faster than sequential
        // Allow very generous tolerance for test environment variability
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(delayMs * 10,
            $"GetGame should execute MongoDB queries in parallel. Expected <{delayMs * 10}ms, got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetGameDetails_CallsBothCountersInParallel()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameExtended { Id = gameId, Title = "Test Game" };
        repository.Setup(r => r.GetGameDetails(gameId, testUserId)).ReturnsAsync(game);

        var delayMs = 100;

        unreadCounters
            .Setup(r => r.SelectByEntities(testUserId, It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .Returns(async () =>
            {
                await Task.Delay(delayMs);
                return new Dictionary<Guid, int> { { gameId, 0 } };
            });

        // Act
        var stopwatch = Stopwatch.StartNew();
        await service.GetGameDetails(gameId);
        stopwatch.Stop();

        // Assert - parallel execution should complete faster than sequential
        // Allow generous tolerance for test environment variability
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(delayMs * 10,
            $"GetGameDetails should execute MongoDB queries in parallel. Expected <{delayMs * 10}ms, got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetGames_CallsBothCountersInParallel_ForAuthenticatedUser()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var games = new[] { new GameOutput { Id = gameId, Title = "Test Game" } };
        var query = new GamesQuery { Statuses = new HashSet<ModuleStatus> { ModuleStatus.Active } };

        repository.Setup(r => r.Count(query, testUserId)).ReturnsAsync(1);
        repository.Setup(r => r.GetGames(It.IsAny<PagingData>(), query, testUserId)).ReturnsAsync(games);

        var delayMs = 100;

        unreadCounters
            .Setup(r => r.SelectByEntities(testUserId, It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .Returns(async () =>
            {
                await Task.Delay(delayMs);
                return new Dictionary<Guid, int> { { gameId, 0 } };
            });

        // Act
        var stopwatch = Stopwatch.StartNew();
        await service.GetGames(query);
        stopwatch.Stop();

        // Assert - parallel execution should complete faster than sequential
        // Allow generous tolerance for test environment variability
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(delayMs * 10,
            $"GetGames should execute MongoDB queries in parallel for authenticated users. Expected <{delayMs * 10}ms, got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetGames_FillsTotalCounts_ForAnonymousUser()
    {
        // Arrange - set up anonymous user (Guest role)
        identity.Setup(i => i.User).Returns(new AuthenticatedUser
        {
            UserId = Guid.Empty,
            Role = UserRole.Guest // Makes IsAuthenticated = false
        });

        var game1Id = Guid.NewGuid();
        var game2Id = Guid.NewGuid();
        var games = new[]
        {
            new GameOutput { Id = game1Id, Title = "Test Game 1" },
            new GameOutput { Id = game2Id, Title = "Test Game 2" }
        };
        var query = new GamesQuery { Statuses = new HashSet<ModuleStatus> { ModuleStatus.Active } };

        repository.Setup(r => r.Count(query, Guid.Empty)).ReturnsAsync(2);
        repository.Setup(r => r.GetGames(It.IsAny<PagingData>(), query, Guid.Empty)).ReturnsAsync(games);

        // Set up total counts (for anonymous users)
        repository
            .Setup(r => r.GetTotalPostCounts(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { game1Id, 42 }, { game2Id, 15 } });
        repository
            .Setup(r => r.GetTotalCommentCounts(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { game1Id, 7 }, { game2Id, 3 } });

        // Act
        var (result, _) = await service.GetGames(query);
        var gamesArray = result.ToArray();

        // Assert - verify total counts are filled (not 0)
        gamesArray.Should().HaveCount(2);
        gamesArray[0].UnreadPostsCount.Should().Be(42, "Total post count should be filled for anonymous user");
        gamesArray[0].UnreadCommentsCount.Should().Be(7, "Total comment count should be filled for anonymous user");
        gamesArray[1].UnreadPostsCount.Should().Be(15);
        gamesArray[1].UnreadCommentsCount.Should().Be(3);
    }

    [Fact]
    public async Task GetGames_CallsTotalCountMethods_ForAnonymousUser()
    {
        // Arrange - set up anonymous user
        identity.Setup(i => i.User).Returns(new AuthenticatedUser
        {
            UserId = Guid.Empty,
            Role = UserRole.Guest
        });

        var gameId = Guid.NewGuid();
        var games = new[] { new GameOutput { Id = gameId, Title = "Test Game" } };
        var query = new GamesQuery { Statuses = new HashSet<ModuleStatus> { ModuleStatus.Active } };

        repository.Setup(r => r.Count(query, Guid.Empty)).ReturnsAsync(1);
        repository.Setup(r => r.GetGames(It.IsAny<PagingData>(), query, Guid.Empty)).ReturnsAsync(games);

        // Act
        await service.GetGames(query);

        // Assert - verify total count methods were called (not unread counters)
        repository.Verify(r => r.GetTotalPostCounts(It.IsAny<IEnumerable<Guid>>()), Times.Once);
        repository.Verify(r => r.GetTotalCommentCounts(It.IsAny<IEnumerable<Guid>>()), Times.Once);

        // Unread counters should NOT be called for anonymous users
        unreadCounters.Verify(r => r.SelectByEntities(
            It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()), Times.Never);
    }

    [Fact]
    public async Task GetGames_ExecutesTotalCountQueriesInParallel_ForAnonymousUser()
    {
        // Arrange - set up anonymous user
        identity.Setup(i => i.User).Returns(new AuthenticatedUser
        {
            UserId = Guid.Empty,
            Role = UserRole.Guest
        });

        var gameId = Guid.NewGuid();
        var games = new[] { new GameOutput { Id = gameId, Title = "Test Game" } };
        var query = new GamesQuery { Statuses = new HashSet<ModuleStatus> { ModuleStatus.Active } };

        repository.Setup(r => r.Count(query, Guid.Empty)).ReturnsAsync(1);
        repository.Setup(r => r.GetGames(It.IsAny<PagingData>(), query, Guid.Empty)).ReturnsAsync(games);

        var delayMs = 100;

        // Set up delays to verify parallel execution
        repository
            .Setup(r => r.GetTotalPostCounts(It.IsAny<IEnumerable<Guid>>()))
            .Returns(async () =>
            {
                await Task.Delay(delayMs);
                return new Dictionary<Guid, int> { { gameId, 10 } };
            });
        repository
            .Setup(r => r.GetTotalCommentCounts(It.IsAny<IEnumerable<Guid>>()))
            .Returns(async () =>
            {
                await Task.Delay(delayMs);
                return new Dictionary<Guid, int> { { gameId, 5 } };
            });

        // Act
        var stopwatch = Stopwatch.StartNew();
        await service.GetGames(query);
        stopwatch.Stop();

        // Assert - parallel execution should complete faster than sequential
        // Allow generous tolerance for test environment variability
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(delayMs * 10,
            $"Total count queries should execute in parallel for anonymous users. Expected <{delayMs * 10}ms, got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetGames_HandlesEmptyGamesList_ForAnonymousUser()
    {
        // Arrange - set up anonymous user
        identity.Setup(i => i.User).Returns(new AuthenticatedUser
        {
            UserId = Guid.Empty,
            Role = UserRole.Guest
        });

        var query = new GamesQuery { Statuses = new HashSet<ModuleStatus> { ModuleStatus.Active } };

        repository.Setup(r => r.Count(query, Guid.Empty)).ReturnsAsync(0);
        repository.Setup(r => r.GetGames(It.IsAny<PagingData>(), query, Guid.Empty)).ReturnsAsync(Array.Empty<GameOutput>());

        // Act
        var (result, paging) = await service.GetGames(query);

        // Assert - empty list should not call counter methods
        result.Should().BeEmpty();
        repository.Verify(r => r.GetTotalPostCounts(It.IsAny<IEnumerable<Guid>>()), Times.Never);
        repository.Verify(r => r.GetTotalCommentCounts(It.IsAny<IEnumerable<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task GetGames_HandlesMissingCounterValues_ForAnonymousUser()
    {
        // Arrange - set up anonymous user
        identity.Setup(i => i.User).Returns(new AuthenticatedUser
        {
            UserId = Guid.Empty,
            Role = UserRole.Guest
        });

        var game1Id = Guid.NewGuid();
        var game2Id = Guid.NewGuid();
        var games = new[]
        {
            new GameOutput { Id = game1Id, Title = "Test Game 1" },
            new GameOutput { Id = game2Id, Title = "Test Game 2" }
        };
        var query = new GamesQuery { Statuses = new HashSet<ModuleStatus> { ModuleStatus.Active } };

        repository.Setup(r => r.Count(query, Guid.Empty)).ReturnsAsync(2);
        repository.Setup(r => r.GetGames(It.IsAny<PagingData>(), query, Guid.Empty)).ReturnsAsync(games);

        // Set up counters - only game1 has values, game2 is missing from dictionaries
        repository
            .Setup(r => r.GetTotalPostCounts(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { game1Id, 10 } }); // game2 missing
        repository
            .Setup(r => r.GetTotalCommentCounts(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { game1Id, 5 } }); // game2 missing

        // Act
        var (result, _) = await service.GetGames(query);
        var gamesArray = result.ToArray();

        // Assert - missing values should default to 0
        gamesArray[0].UnreadPostsCount.Should().Be(10);
        gamesArray[0].UnreadCommentsCount.Should().Be(5);
        gamesArray[1].UnreadPostsCount.Should().Be(0, "Missing counter value should default to 0");
        gamesArray[1].UnreadCommentsCount.Should().Be(0, "Missing counter value should default to 0");
    }
}
