using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostPendencies;
using DM.Domain.Game.Features.Rooms;
using DM.Testing.Dsl;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostPendencies;

public class PostPendencyServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IRoomService _roomService;
    private readonly IPostPendencyRepository _repository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IPostPendencyFactory _factory;
    private readonly PostPendencyService _service;

    public PostPendencyServiceShould()
    {
        var validator = Mock<IValidator<CreatePostPendency>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<CreatePostPendency>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _roomService = Mock<IRoomService>();

        var factory = _factory = Mock<IPostPendencyFactory>();
        factory.Create(Arg.Any<CreatePostPendency>(), Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(new CreatePostPendencyEntity());

        _repository = Mock<IPostPendencyRepository>();

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(Guid.NewGuid(), "testuser");
        _identityProvider.Current.Returns(identity);

        _service = new PostPendencyService(
            validator,
            _roomService,
            _intentionManager,
            factory,
            _repository,
            _producer,
            _identityProvider);
    }

    [Fact]
    public async Task AuthorizeCreatePostPendencyAction()
    {
        var characterId = Guid.NewGuid();
        var createPendency = new CreatePostPendency { RoomId = Guid.NewGuid(), CharacterId = characterId };
        var room = CreateRoomWithAccesses(characterId, Guid.NewGuid());

        _roomService.GetWithGameAsync(Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostPendencyEntity>()).Returns(new PostPendency());

        await _service.CreateAsync(createPendency);

        _intentionManager.Received(1).ThrowIfForbidden(RoomIntention.CreatePostPendency, room);
    }

    [Fact]
    public async Task PreventDuplicatePendencyForSameUser()
    {
        var waitingForUserId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var currentUserId = _identityProvider.Current.User.UserId;
        var createPendency = new CreatePostPendency { RoomId = Guid.NewGuid(), CharacterId = characterId };

        var room = new RoomToUpdate
        {
            Game = new GameDto(),
            Pendencies = new List<PostPendency>
            {
                new PostPendency
                {
                    CreatedBy = new GeneralUser { UserId = currentUserId },
                    WaitingForUser = new GeneralUser { UserId = waitingForUserId }
                }
            },
            Accesses = new List<RoomAccess>
            {
                new RoomAccess
                {
                    Character = new Character
                    {
                        Id = characterId,
                        Author = new GeneralUser { UserId = waitingForUserId, Username = "targetuser" }
                    }
                }
            }
        };

        _roomService.GetWithGameAsync(Arg.Any<Guid>()).Returns(room);

        var act = async () => await _service.CreateAsync(createPendency);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreatePendencyAndPublishEvent()
    {
        var pendencyId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createPendency = new CreatePostPendency { RoomId = Guid.NewGuid(), CharacterId = characterId };
        var room = CreateRoomWithAccesses(characterId, Guid.NewGuid());

        _roomService.GetWithGameAsync(Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostPendencyEntity>()).Returns(new PostPendency { Id = pendencyId });

        await _service.CreateAsync(createPendency);

        await _repository.Received(1).Create(Arg.Any<CreatePostPendencyEntity>());
        await _producer.Received(1).SendAsync(EventType.RoomPendencyCreated, pendencyId);
    }

    /// <summary>
    /// The site names the character and nothing else: the one waited for is
    /// the player of that character, and the one who waits is the caller.
    /// </summary>
    [Fact]
    public async Task WaitForThePlayerOfTheCharacterOnBehalfOfTheCaller()
    {
        var characterId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var createPendency = new CreatePostPendency { RoomId = Guid.NewGuid(), CharacterId = characterId };
        var room = CreateRoomWithAccesses(characterId, playerId);
        room.Accesses = room.Accesses.Append(new RoomAccess
        {
            Character = new Character { Id = Guid.NewGuid(), Author = new GeneralUser { UserId = Guid.NewGuid() } }
        }).ToList();

        _roomService.GetWithGameAsync(Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostPendencyEntity>()).Returns(new PostPendency());

        await _service.CreateAsync(createPendency);

        _factory.Received(1).Create(createPendency, _identityProvider.Current.User.UserId, playerId);
    }

    /// <summary>
    /// A character the room does not let in, or one with no player behind it,
    /// is nobody to wait for.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefuseACharacterNobodyCanBeWaitedOnThrough(bool inTheRoom)
    {
        var characterId = Guid.NewGuid();
        var createPendency = new CreatePostPendency { RoomId = Guid.NewGuid(), CharacterId = characterId };
        var room = inTheRoom
            ? CreateRoomWithAccesses(characterId, playerId: null)
            : CreateRoomWithAccesses(Guid.NewGuid(), Guid.NewGuid());

        _roomService.GetWithGameAsync(Arg.Any<Guid>()).Returns(room);

        var act = async () => await _service.CreateAsync(createPendency);

        await act.Should().ThrowAsync<HttpBadRequestException>();
        await _repository.DidNotReceive().Create(Arg.Any<CreatePostPendencyEntity>());
    }

    [Fact]
    public async Task AuthorizeDeletePostPendencyAction()
    {
        var pendencyId = Guid.NewGuid();
        var pendency = new PostPendency { Id = pendencyId };

        _repository.Get(pendencyId).Returns(pendency);
        _repository.Delete(pendencyId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(pendencyId);

        _intentionManager.Received(1).ThrowIfForbidden(RoomIntention.DeletePostPendency, pendency);
    }

    [Fact]
    public async Task DeletePendencyAndPublishEvent()
    {
        var pendencyId = Guid.NewGuid();
        var pendency = new PostPendency { Id = pendencyId };

        _repository.Get(pendencyId).Returns(pendency);
        _repository.Delete(pendencyId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(pendencyId);

        await _repository.Received(1).Delete(pendencyId);
        await _producer.Received(1).SendAsync(EventType.RoomPendencyDeleted, pendencyId);
    }

    /// <summary>
    /// The projection that carries the game, which is what the permission rule
    /// reads: asked with the flat one the manager finds no resolver and refuses
    /// everybody.
    /// </summary>
    private static RoomToUpdate CreateRoomWithAccesses(Guid characterId, Guid? playerId) => new()
    {
        Game = new GameDto(),
        Pendencies = new List<PostPendency>(),
        Accesses = new List<RoomAccess>
        {
            new RoomAccess
            {
                Character = new Character
                {
                    Id = characterId,
                    Author = playerId == null ? null : new GeneralUser { UserId = playerId.Value, Username = "targetuser" }
                }
            }
        }
    };
}
