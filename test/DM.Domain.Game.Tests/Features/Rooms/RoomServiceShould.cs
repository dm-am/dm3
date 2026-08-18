using System;
using System.Collections.Generic;
using System.Linq;
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
using DM.Testing.Dsl;
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
    private readonly Mock<IGuidFactory> _guidFactory;
    private static readonly Guid CurrentUserId = Guid.NewGuid();

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
        var identity = Identities.User(CurrentUserId, "testuser");
        _identityProvider.Setup(p => p.Current).Returns(identity);

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _service = new RoomService(
            _gameService.Object,
            createValidator.Object,
            updateValidator.Object,
            _intentionManager.Object,
            _repository.Object,
            _unreadCountersRepository.Object,
            _producer.Object,
            _identityProvider.Object,
            _guidFactory.Object);
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

        // The identifier is minted here and not learnt from the row: the marker is
        // written before the row exists to return one.
        _guidFactory.Setup(f => f.Create()).Returns(roomId);

        await _service.CreateAsync(createRoom);

        _unreadCountersRepository.Verify(r => r.CreateMarkerAsync(roomId, gameId, UnreadEntryType.Message), Times.Once);
        _unreadCountersRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()),
            Times.Never, "the row landed, so the reservation was committed");
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

        _repository.Verify(r => r.Update(It.Is<UpdateRoomEntity>(e =>
            e.RoomId == roomId && e.Title == "Updated Room")), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.ChangedRoom, roomId), Times.Once);
    }

    /// <summary>
    /// Every patch field the caller sends reaches the repository.
    /// </summary>
    /// <remarks>
    /// The service copies the fields by hand and the suite asserted one of them,
    /// through It.IsAny on the rest: dropping Title stopped renaming a room and
    /// left a test literally named "update room" green. The captured entity is
    /// compared field by field, and the guard below fails when a field is added
    /// with nobody asserting it.
    /// </remarks>
    [Fact]
    public async Task CarryEveryPatchFieldThroughToTheRepository()
    {
        var roomId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var previousRoomId = Guid.NewGuid();
        var updateRoom = new UpdateRoom
        {
            RoomId = roomId,
            Title = "Updated Room",
            Type = RoomType.Chat,
            AccessType = RoomAccessType.Private,
            PreviousRoomId = Optional<Guid>.WithValue(previousRoomId),
            ViewPrivateText = true,
            ViewDiceResults = true,
            DiceEnabled = false,
            HiddenWithoutAccess = true,
            IsArchived = true,
            IsRemoved = false,
            ChatId = chatId
        };
        var room = new RoomToUpdate
        {
            Id = roomId,
            Game = new GameDto
            {
                Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
            }
        };

        UpdateRoomEntity? captured = null;
        _repository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Update(It.IsAny<UpdateRoomEntity>()))
            .Callback<UpdateRoomEntity>(entity => captured = entity)
            .ReturnsAsync(room);

        await _service.UpdateAsync(updateRoom);

        captured.Should().NotBeNull();
        captured!.RoomId.Should().Be(roomId);
        captured.Title.Should().Be("Updated Room");
        captured.Type.Should().Be(RoomType.Chat);
        captured.AccessType.Should().Be(RoomAccessType.Private);
        captured.NewPreviousRoomId.Should().Be(previousRoomId);
        captured.ShouldReorder.Should().BeTrue();
        captured.ViewPrivateText.Should().BeTrue();
        captured.ViewDiceResults.Should().BeTrue();
        captured.DiceEnabled.Should().BeFalse();
        captured.HiddenWithoutAccess.Should().BeTrue();
        captured.IsArchived.Should().BeTrue();
        captured.ChatId.Should().Be(chatId);
        captured.ShouldSetChatId.Should().BeTrue();
        captured.IsRemoved.Should().BeFalse();
    }

    /// <summary>A field added to the patch entity has to be asserted above.</summary>
    [Fact]
    public void AssertEveryFieldThePatchEntityCarries()
    {
        typeof(UpdateRoomEntity).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            new[]
            {
                nameof(UpdateRoomEntity.RoomId), nameof(UpdateRoomEntity.Title),
                nameof(UpdateRoomEntity.Type), nameof(UpdateRoomEntity.AccessType),
                nameof(UpdateRoomEntity.NewPreviousRoomId), nameof(UpdateRoomEntity.ShouldReorder),
                nameof(UpdateRoomEntity.ViewPrivateText), nameof(UpdateRoomEntity.ViewDiceResults),
                nameof(UpdateRoomEntity.DiceEnabled), nameof(UpdateRoomEntity.HiddenWithoutAccess),
                nameof(UpdateRoomEntity.IsArchived), nameof(UpdateRoomEntity.ChatId),
                nameof(UpdateRoomEntity.ShouldSetChatId), nameof(UpdateRoomEntity.IsRemoved)
            },
            "CarryEveryPatchFieldThroughToTheRepository asserts each of these by name, " +
            "so a new field silently unasserted is what this refuses");
    }

    [Fact]
    public async Task PassIsArchivedThroughToRepositoryOnUpdate()
    {
        var roomId = Guid.NewGuid();
        var updateRoom = new UpdateRoom { RoomId = roomId, IsArchived = true };
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

        _repository.Verify(r => r.Update(It.Is<UpdateRoomEntity>(e => e.IsArchived == true)), Times.Once);
    }

    [Fact]
    public async Task LeaveIsArchivedUnsetWhenUpdateOmitsIt()
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

        _repository.Verify(r => r.Update(It.Is<UpdateRoomEntity>(e => e.IsArchived == null)), Times.Once);
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
        _repository.Setup(r => r.Delete(roomId, CurrentUserId)).Returns(Task.CompletedTask);

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
        _repository.Setup(r => r.Delete(roomId, CurrentUserId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(roomId);

        // With the author, because the schema carries a column for it and the moderation
        // screens read it: a room removed by a nameless somebody is the defect.
        _repository.Verify(r => r.Delete(roomId, CurrentUserId), Times.Once);
        _unreadCountersRepository.Verify(r => r.DeleteAsync(roomId, UnreadEntryType.Message), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.DeletedRoom, roomId), Times.Once);
    }

    /// <summary>
    /// The rank that opens a premoderated game reaches the storage scope of its
    /// rooms, and it is the intention that answers it — not a second comparison of
    /// roles inside the service.
    /// </summary>
    /// <remarks>
    /// Both directions, because the parameter defaults to false: passing it always
    /// would fill the room list of a game nobody is judging, and never would leave
    /// the judge the empty list this fixes.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScopeTheRoomListByThePremoderationVerdictIntention(bool mayJudge)
    {
        var gameId = Guid.NewGuid();
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _intentionManager.Setup(m => m.IsAllowed(GameIntention.SetStatusModeration)).Returns(mayJudge);
        _repository
            .Setup(r => r.GetAllVisible(gameId, CurrentUserId, It.IsAny<bool>()))
            .ReturnsAsync(Array.Empty<Room>());

        await _service.GetAllAsync(gameId);

        _repository.Verify(r => r.GetAllVisible(gameId, CurrentUserId, mayJudge), Times.Once);
    }

    /// <summary>
    /// The same rank on the point read, or the menu would name rooms whose own
    /// address then answered 404.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScopeASingleRoomByThePremoderationVerdictIntention(bool mayJudge)
    {
        var roomId = Guid.NewGuid();
        _intentionManager.Setup(m => m.IsAllowed(GameIntention.SetStatusModeration)).Returns(mayJudge);
        _repository
            .Setup(r => r.GetAvailable(roomId, CurrentUserId, It.IsAny<bool>()))
            .ReturnsAsync(new Room { Id = roomId });
        _unreadCountersRepository
            .Setup(r => r.SelectByEntitiesAsync(CurrentUserId, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [roomId] = 0 });

        await _service.GetAsync(roomId);

        _repository.Verify(r => r.GetAvailable(roomId, CurrentUserId, mayJudge), Times.Once);
    }
}
