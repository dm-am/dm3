using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IGameRepository> _repository;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly GameService _service;
    private readonly Guid _currentUserId;

    public GameServiceShould()
    {
        var gamesQueryValidator = Mock<IValidator<GamesQuery>>();
        gamesQueryValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<GamesQuery>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateGameValidator = Mock<IValidator<UpdateGame>>();
        updateGameValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateGame>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var creationValidator = Mock<IGameCreationValidator>();
        creationValidator.Setup(v => v.ValidateAndAuthorize(It.IsAny<CreateGame>()))
            .Returns(Task.CompletedTask);

        var dataResolver = Mock<IGameCreationDataResolver>();
        dataResolver.Setup(r => r.GetAvailableTagIds())
            .ReturnsAsync(Array.Empty<Guid>());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));

        var schemaService = Mock<IAttributeSchemaService>();

        _repository = Mock<IGameRepository>();

        var userRepository = Mock<IGameUserRepository>();

        var invitationService = Mock<IGameInvitationService>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        var userBlacklistChecker = Mock<DM.Domain.Core.Blacklists.IUserBlacklistChecker>();
        userBlacklistChecker.Setup(c => c.GetBlockedUserIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var gameBlacklistRepository = Mock<IGameBlacklistRepository>();

        var unreadCountersRepository = Mock<IUnreadCountersRepository>();
        unreadCountersRepository.Setup(r => r.CreateAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);
        unreadCountersRepository.Setup(r => r.SelectByEntitiesAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .ReturnsAsync((Guid userId, UnreadEntryType type, Guid[] ids) =>
                ids.ToDictionary(id => id, _ => 0) as IDictionary<Guid, int>);

        var subscriptionService = Mock<IGameSubscriptionService>();

        var roomRepository = Mock<IRoomRepository>();

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        var intentionConverter = Mock<IGameIntentionConverter>();
        intentionConverter.Setup(c => c.Convert(It.IsAny<ModuleStatus>()))
            .Returns((GameIntention.Edit, EventType.ChangedGame));

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _producer.Setup(p => p.SendAsync(It.IsAny<IEnumerable<EventType>>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        var cache = Mock<IMemoryCache>();
        var cacheEntry = Mock<ICacheEntry>();
        cache.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(cacheEntry.Object);

        var logger = Mock<ILogger<GameService>>();

        _service = new GameService(
            gamesQueryValidator.Object,
            updateGameValidator.Object,
            creationValidator.Object,
            dataResolver.Object,
            _intentionManager.Object,
            schemaService.Object,
            _repository.Object,
            userRepository.Object,
            invitationService.Object,
            _identityProvider.Object,
            userBlacklistChecker.Object,
            gameBlacklistRepository.Object,
            unreadCountersRepository.Object,
            subscriptionService.Object,
            roomRepository.Object,
            _dateTimeProvider.Object,
            _guidFactory.Object,
            intentionConverter.Object,
            _producer.Object,
            cache.Object,
            logger.Object);
    }

    [Fact]
    public async Task CreateGameAndPublishEvent()
    {
        var createGame = new CreateGame { Title = "Test Game", SystemName = "Test System" };
        var gameId = Guid.NewGuid();
        var game = new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = Guid.NewGuid() } } };
        _guidFactory.SetupSequence(g => g.Create())
            .Returns(gameId)
            .Returns(Guid.NewGuid());
        _repository.Setup(r => r.Create(It.IsAny<CreateGameEntity>(), It.IsAny<CreateRoomEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var result = await _service.CreateAsync(createGame);

        result.Should().NotBeNull();
        _producer.Verify(p => p.SendAsync(EventType.NewGame, gameId), Times.Once);
    }

    [Fact]
    public async Task ThrowNotFoundWhenGameDoesNotExist()
    {
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.GetGame(gameId, _currentUserId, It.IsAny<CancellationToken>())).ReturnsAsync((GameDto?)null);

        var act = async () => await _service.GetAsync(gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Gone);
    }

    [Fact]
    public async Task AuthorizeAndReturnTheGameOnRead()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };
        _repository.Setup(r => r.GetGame(gameId, _currentUserId, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _service.GetAsync(gameId);

        result.Should().BeSameAs(game);
        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Read, game), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUpdateAndAnnounceTheChange()
    {
        var gameId = Guid.NewGuid();
        var updateGame = new UpdateGame { GameId = gameId, Title = "Updated Game" };
        var game = new GameDetails { Id = gameId, Recruitment = new GameRecruitment() };
        _repository.Setup(r => r.GetGameDetails(gameId, _currentUserId, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _repository.Setup(r => r.Update(It.IsAny<UpdateGameEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _service.UpdateAsync(updateGame);

        result.Should().BeSameAs(game);
        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Once);
        // An edit that changes no status announces exactly the one event
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.SequenceEqual(new[] { EventType.ChangedGame })), gameId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeDeleteAndAnnounceIt()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDetails
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            Recruitment = new GameRecruitment()
        };
        _repository.Setup(r => r.GetGameDetails(gameId, _currentUserId, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _repository.Setup(r => r.Delete(gameId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _service.DeleteAsync(gameId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Delete, game), Times.Once);
        _repository.Verify(r => r.Delete(gameId, It.IsAny<CancellationToken>()), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.DeletedGame, gameId), Times.Once);
    }
}
