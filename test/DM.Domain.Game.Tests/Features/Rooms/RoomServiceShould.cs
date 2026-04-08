using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Core.Users;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Rooms;

public class RoomServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IGameService> _gameService;
    private readonly Mock<IRoomRepository> _repository;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly RoomService _service;

    public RoomServiceShould()
    {
        _gameService = Mock<IGameService>();

        var createValidator = Mock<IValidator<CreateRoom>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateRoom>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateRoom>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateRoom>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));

        _repository = Mock<IRoomRepository>();

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.User(Guid.NewGuid(), "testuser");
        _identityProvider.Setup(p => p.Current).Returns(identity);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _service = new RoomService(
            _gameService.Object,
            createValidator.Object,
            updateValidator.Object,
            _intentionManager.Object,
            _repository.Object,
            _unreadCountersRepository.Object,
            _producer.Object,
            _identityProvider.Object,
            guidFactory.Object);
    }

    [Fact]
    public async Task AuthorizeCreateRoomAction()
    {
        var gameId = Guid.NewGuid();
        var createRoom = new CreateRoom { GameId = gameId, Title = "Test Room" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.GetLastRoomInfo(gameId)).ReturnsAsync(new RoomOrderInfo { OrderNumber = 0 });
        _repository.Setup(r => r.Create(It.IsAny<CreateRoomEntity>()))
            .ReturnsAsync(new Room { Id = Guid.NewGuid(), GameId = gameId });

        await _service.CreateAsync(createRoom);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Once);
    }

    [Fact]
    public async Task CreateRoomWithIncrementedOrderNumber()
    {
        var gameId = Guid.NewGuid();
        var createRoom = new CreateRoom { GameId = gameId, Title = "Test Room" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };
        var lastOrderNumber = 5;

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.GetLastRoomInfo(gameId)).ReturnsAsync(new RoomOrderInfo { OrderNumber = lastOrderNumber });
        _repository.Setup(r => r.Create(It.IsAny<CreateRoomEntity>()))
            .ReturnsAsync(new Room { Id = Guid.NewGuid(), GameId = gameId });

        await _service.CreateAsync(createRoom);

        _repository.Verify(r => r.Create(It.Is<CreateRoomEntity>(e => e.OrderNumber == lastOrderNumber + 1)), Times.Once);
    }

    [Fact]
    public async Task CreateRoomAndInitializeUnreadCounters()
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createRoom = new CreateRoom { GameId = gameId, Title = "Test Room" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.GetLastRoomInfo(gameId)).ReturnsAsync(new RoomOrderInfo { OrderNumber = 0 });
        _repository.Setup(r => r.Create(It.IsAny<CreateRoomEntity>()))
            .ReturnsAsync(new Room { Id = roomId, GameId = gameId });

        await _service.CreateAsync(createRoom);

        _unreadCountersRepository.Verify(r => r.CreateAsync(roomId, gameId, UnreadEntryType.Message), Times.Once);
    }

    [Fact]
    public async Task PublishNewRoomEvent()
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createRoom = new CreateRoom { GameId = gameId, Title = "Test Room" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.GetLastRoomInfo(gameId)).ReturnsAsync(new RoomOrderInfo { OrderNumber = 0 });
        _repository.Setup(r => r.Create(It.IsAny<CreateRoomEntity>()))
            .ReturnsAsync(new Room { Id = roomId, GameId = gameId });

        await _service.CreateAsync(createRoom);

        _producer.Verify(p => p.SendAsync(EventType.NewRoom, roomId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUpdateRoomAction()
    {
        var roomId = Guid.NewGuid();
        var updateRoom = new UpdateRoom { RoomId = roomId, Title = "Updated Room" };
        var game = new GameDto
        {
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };
        var room = new RoomToUpdate { Id = roomId, Game = game };

        _repository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Update(It.IsAny<UpdateRoomEntity>())).ReturnsAsync(room);

        await _service.UpdateAsync(updateRoom);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Once);
    }

    [Fact]
    public async Task UpdateRoomAndPublishEvent()
    {
        var roomId = Guid.NewGuid();
        var updateRoom = new UpdateRoom { RoomId = roomId, Title = "Updated Room" };
        var room = new RoomToUpdate
        {
            Id = roomId,
            Game = new GameDto
            {
                Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
            }
        };

        _repository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Update(It.IsAny<UpdateRoomEntity>())).ReturnsAsync(room);

        await _service.UpdateAsync(updateRoom);

        _repository.Verify(r => r.Update(It.IsAny<UpdateRoomEntity>()), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.ChangedRoom, roomId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeDeleteRoomAction()
    {
        var roomId = Guid.NewGuid();
        var game = new GameDto
        {
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };
        var room = new RoomToUpdate { Id = roomId, Game = game };

        _repository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Delete(roomId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(roomId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Once);
    }

    [Fact]
    public async Task DeleteRoomAndCleanupUnreadCounters()
    {
        var roomId = Guid.NewGuid();
        var room = new RoomToUpdate
        {
            Id = roomId,
            Game = new GameDto
            {
                Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
            }
        };

        _repository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Delete(roomId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(roomId);

        _repository.Verify(r => r.Delete(roomId), Times.Once);
        _unreadCountersRepository.Verify(r => r.DeleteAsync(roomId, UnreadEntryType.Message), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.DeletedRoom, roomId), Times.Once);
    }
}
