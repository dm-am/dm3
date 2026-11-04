using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Domain.Game.Features.Rooms;
using DM.Testing.Dsl;
using DM.Testing;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.Core;
using Xunit;

namespace DM.Domain.Game.Tests.Features.RoomAccesses;

public class RoomAccessServiceShould : UnitTestBase
{
    private readonly IIntentionManager intentionManager;
    private readonly IRoomRepository roomRepository;
    private readonly IRoomAccessRepository repository;
    private readonly IEventProducer producer;
    private readonly IIdentityProvider identityProvider;
    private readonly RoomAccessService service;

    public RoomAccessServiceShould()
    {
        var createValidator = Mock<IValidator<CreateRoomAccess>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateRoomAccess>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateRoomAccess>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateRoomAccess>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        roomRepository = Mock<IRoomRepository>();

        intentionManager = Mock<IIntentionManager>();

        var factory = Mock<IRoomAccessFactory>();
        factory.CreateForCharacter(Arg.Any<CreateRoomAccess>(), Arg.Any<Guid>()).Returns(EntityOfTheRequest);
        factory.CreateForReader(Arg.Any<CreateRoomAccess>(), Arg.Any<Guid>()).Returns(EntityOfTheRequest);

        var characterClaimApprove = Mock<ICharacterClaimApprove>();
        characterClaimApprove.GetCharacterId(Arg.Any<Guid>(), Arg.Any<RoomToUpdate>()).Returns(Guid.NewGuid());

        var readerClaimApprove = Mock<IReaderClaimApprove>();
        readerClaimApprove.GetReaderUserId(Arg.Any<string>(), Arg.Any<RoomToUpdate>()).Returns(Guid.NewGuid());

        repository = Mock<IRoomAccessRepository>();

        producer = Mock<IEventProducer>();
        producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(Guid.NewGuid(), "testuser");
        identityProvider.Current.Returns(identity);

        service = new RoomAccessService(
            createValidator,
            updateValidator,
            roomRepository,
            intentionManager,
            factory,
            characterClaimApprove,
            readerClaimApprove,
            repository,
            producer,
            identityProvider);
    }

    private static CreateRoomAccessEntity EntityOfTheRequest(CallInfo call) =>
        new() { RoomId = call.ArgAt<CreateRoomAccess>(0).RoomId };

    [Fact]
    public async Task AuthorizeCreateRoomAccessAction()
    {
        var roomId = Guid.NewGuid();
        var createAccess = new CreateRoomAccess { RoomId = roomId, CharacterId = Guid.NewGuid() };
        var game = new GameDto();
        var room = new RoomToUpdate { Id = roomId, Game = game };

        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Any<CreateRoomAccessEntity>()).Returns(new RoomAccess { RoomId = roomId });

        await service.CreateAsync(createAccess);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task CreateRoomAccessAndPublishEvent()
    {
        var roomId = Guid.NewGuid();
        var createAccess = new CreateRoomAccess { RoomId = roomId, CharacterId = Guid.NewGuid() };
        var room = new RoomToUpdate { Id = roomId, Game = new GameDto() };

        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Any<CreateRoomAccessEntity>()).Returns(new RoomAccess { RoomId = roomId });

        await service.CreateAsync(createAccess);

        await repository.Received(1).Create(Arg.Any<CreateRoomAccessEntity>());
        await producer.Received(1).SendAsync(EventType.ChangedRoom, roomId);
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

        repository.GetAccess(accessId, Arg.Any<Guid>()).Returns(access);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Update(Arg.Any<UpdateRoomAccessEntity>()).Returns(new RoomAccess { RoomId = roomId });

        await service.UpdateAsync(updateAccess);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task UpdateRoomAccessAndPublishEvent()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var updateAccess = new UpdateRoomAccess { AccessId = accessId, Policy = RoomAccessPolicy.ReadOnly };
        var room = new RoomToUpdate { Id = roomId, Game = new GameDto() };
        var access = new RoomAccess { Id = accessId, RoomId = roomId, Character = new Character() };

        repository.GetAccess(accessId, Arg.Any<Guid>()).Returns(access);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Update(Arg.Any<UpdateRoomAccessEntity>()).Returns(new RoomAccess { RoomId = roomId });

        await service.UpdateAsync(updateAccess);

        await repository.Received(1).Update(Arg.Any<UpdateRoomAccessEntity>());
        await producer.Received(1).SendAsync(EventType.ChangedRoom, roomId);
    }

    /// <summary>
    /// Raising a character to writing is the point of the policy, and it was the one
    /// thing the endpoint refused. The guard read User to mean "this is a reader",
    /// but the projection fills User for a character row too, from the character's
    /// author, so PATCH Full answered 400 for every row in the table. The fixture
    /// omitted User, which is why nothing caught it.
    /// </summary>
    [Fact]
    public async Task RaiseACharacterAccessToWriting()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var updateAccess = new UpdateRoomAccess { AccessId = accessId, Policy = RoomAccessPolicy.Full };
        var room = new RoomToUpdate { Id = roomId, Game = new GameDto() };
        var access = new RoomAccess
        {
            Id = accessId,
            RoomId = roomId,
            TargetType = RoomAccessTargetType.Character,
            Policy = RoomAccessPolicy.ReadOnly,
            Character = new Character(),
            User = new GeneralUser { UserId = Guid.NewGuid() }
        };

        repository.GetAccess(accessId, Arg.Any<Guid>()).Returns(access);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Update(Arg.Any<UpdateRoomAccessEntity>()).Returns(new RoomAccess { RoomId = roomId });

        await service.UpdateAsync(updateAccess);

        await repository.Received(1).Update(Arg.Is<UpdateRoomAccessEntity>(e => e.Policy == RoomAccessPolicy.Full));
    }

    [Fact]
    public async Task AuthorizeDeleteRoomAccessAction()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var game = new GameDto();
        var room = new RoomToUpdate { Id = roomId, Game = game };
        var access = new RoomAccess { Id = accessId, RoomId = roomId };

        repository.GetAccess(accessId, Arg.Any<Guid>()).Returns(access);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Delete(accessId).Returns(Task.CompletedTask);

        await service.DeleteAsync(accessId);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task DeleteRoomAccessAndPublishEvent()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var room = new RoomToUpdate { Id = roomId, Game = new GameDto() };
        var access = new RoomAccess { Id = accessId, RoomId = roomId };

        repository.GetAccess(accessId, Arg.Any<Guid>()).Returns(access);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Delete(accessId).Returns(Task.CompletedTask);

        await service.DeleteAsync(accessId);

        await repository.Received(1).Delete(accessId);
        await producer.Received(1).SendAsync(EventType.ChangedRoom, roomId);
    }
}
