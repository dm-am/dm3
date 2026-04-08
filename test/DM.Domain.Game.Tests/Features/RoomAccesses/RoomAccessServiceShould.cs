using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Core.Users;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.RoomAccesses;

public class RoomAccessServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IRoomRepository> _roomRepository;
    private readonly Mock<IRoomAccessRepository> _repository;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly RoomAccessService _service;

    public RoomAccessServiceShould()
    {
        var createValidator = Mock<IValidator<CreateRoomAccess>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateRoomAccess>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateRoomAccess>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateRoomAccess>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _roomRepository = Mock<IRoomRepository>();

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));

        var factory = Mock<IRoomAccessFactory>();
        factory.Setup(f => f.CreateForCharacter(It.IsAny<CreateRoomAccess>(), It.IsAny<Guid>()))
            .Returns((CreateRoomAccess req, Guid _) => new CreateRoomAccessEntity { RoomId = req.RoomId });
        factory.Setup(f => f.CreateForReader(It.IsAny<CreateRoomAccess>(), It.IsAny<Guid>()))
            .Returns((CreateRoomAccess req, Guid _) => new CreateRoomAccessEntity { RoomId = req.RoomId });

        var characterClaimApprove = Mock<ICharacterClaimApprove>();
        characterClaimApprove.Setup(c => c.GetCharacterId(It.IsAny<Guid>(), It.IsAny<RoomToUpdate>()))
            .ReturnsAsync(Guid.NewGuid());

        var readerClaimApprove = Mock<IReaderClaimApprove>();
        readerClaimApprove.Setup(r => r.GetReaderUserId(It.IsAny<string>(), It.IsAny<RoomToUpdate>()))
            .ReturnsAsync(Guid.NewGuid());

        _repository = Mock<IRoomAccessRepository>();

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.User(Guid.NewGuid(), "testuser");
        _identityProvider.Setup(p => p.Current).Returns(identity);

        _service = new RoomAccessService(
            createValidator.Object,
            updateValidator.Object,
            _roomRepository.Object,
            _intentionManager.Object,
            factory.Object,
            characterClaimApprove.Object,
            readerClaimApprove.Object,
            _repository.Object,
            _producer.Object,
            _identityProvider.Object);
    }

    [Fact]
    public async Task AuthorizeCreateRoomAccessAction()
    {
        var roomId = Guid.NewGuid();
        var createAccess = new CreateRoomAccess { RoomId = roomId, CharacterId = Guid.NewGuid() };
        var game = new GameDto();
        var room = new RoomToUpdate { Id = roomId, Game = game };

        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreateRoomAccessEntity>()))
            .ReturnsAsync(new RoomAccess { RoomId = roomId });

        await _service.CreateAsync(createAccess);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Once);
    }

    [Fact]
    public async Task CreateRoomAccessAndPublishEvent()
    {
        var roomId = Guid.NewGuid();
        var createAccess = new CreateRoomAccess { RoomId = roomId, CharacterId = Guid.NewGuid() };
        var room = new RoomToUpdate { Id = roomId, Game = new GameDto() };

        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreateRoomAccessEntity>()))
            .ReturnsAsync(new RoomAccess { RoomId = roomId });

        await _service.CreateAsync(createAccess);

        _repository.Verify(r => r.Create(It.IsAny<CreateRoomAccessEntity>()), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.ChangedRoom, roomId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUpdateRoomAccessAction()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var updateAccess = new UpdateRoomAccess { AccessId = accessId, Policy = RoomAccessPolicy.ReadOnly };
        var game = new GameDto();
        var room = new RoomToUpdate { Id = roomId, Game = game };
        var access = new RoomAccess { Id = accessId, RoomId = roomId, Character = new Character() };

        _repository.Setup(r => r.GetAccess(accessId, It.IsAny<Guid>())).ReturnsAsync(access);
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Update(It.IsAny<UpdateRoomAccessEntity>()))
            .ReturnsAsync(new RoomAccess { RoomId = roomId });

        await _service.UpdateAsync(updateAccess);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Once);
    }

    [Fact]
    public async Task UpdateRoomAccessAndPublishEvent()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var updateAccess = new UpdateRoomAccess { AccessId = accessId, Policy = RoomAccessPolicy.ReadOnly };
        var room = new RoomToUpdate { Id = roomId, Game = new GameDto() };
        var access = new RoomAccess { Id = accessId, RoomId = roomId, Character = new Character() };

        _repository.Setup(r => r.GetAccess(accessId, It.IsAny<Guid>())).ReturnsAsync(access);
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Update(It.IsAny<UpdateRoomAccessEntity>()))
            .ReturnsAsync(new RoomAccess { RoomId = roomId });

        await _service.UpdateAsync(updateAccess);

        _repository.Verify(r => r.Update(It.IsAny<UpdateRoomAccessEntity>()), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.ChangedRoom, roomId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeDeleteRoomAccessAction()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var game = new GameDto();
        var room = new RoomToUpdate { Id = roomId, Game = game };
        var access = new RoomAccess { Id = accessId, RoomId = roomId };

        _repository.Setup(r => r.GetAccess(accessId, It.IsAny<Guid>())).ReturnsAsync(access);
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Delete(accessId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(accessId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Once);
    }

    [Fact]
    public async Task DeleteRoomAccessAndPublishEvent()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var room = new RoomToUpdate { Id = roomId, Game = new GameDto() };
        var access = new RoomAccess { Id = accessId, RoomId = roomId };

        _repository.Setup(r => r.GetAccess(accessId, It.IsAny<Guid>())).ReturnsAsync(access);
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Delete(accessId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(accessId);

        _repository.Verify(r => r.Delete(accessId), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.ChangedRoom, roomId), Times.Once);
    }
}
