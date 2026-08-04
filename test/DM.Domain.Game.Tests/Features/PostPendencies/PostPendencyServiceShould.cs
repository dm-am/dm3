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
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostPendencies;
using DM.Domain.Game.Features.Rooms;
using DM.Testing.Dsl;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostPendencies;

public class PostPendencyServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IRoomService> _roomService;
    private readonly Mock<IPostPendencyRepository> _repository;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly PostPendencyService _service;

    public PostPendencyServiceShould()
    {
        var validator = Mock<IValidator<CreatePostPendency>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreatePostPendency>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<RoomIntention>(), It.IsAny<Room>()));

        _roomService = Mock<IRoomService>();

        var factory = Mock<IPostPendencyFactory>();
        factory.Setup(f => f.Create(It.IsAny<CreatePostPendency>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(new CreatePostPendencyEntity());

        _userLookupService = Mock<IUserLookupService>();

        _repository = Mock<IPostPendencyRepository>();

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(Guid.NewGuid(), "testuser");
        _identityProvider.Setup(p => p.Current).Returns(identity);

        _service = new PostPendencyService(
            validator.Object,
            _roomService.Object,
            _intentionManager.Object,
            factory.Object,
            _userLookupService.Object,
            _repository.Object,
            _producer.Object,
            _identityProvider.Object);
    }

    [Fact]
    public async Task AuthorizeCreatePostPendencyAction()
    {
        var targetUserId = Guid.NewGuid();
        var createPendency = new CreatePostPendency { RoomId = Guid.NewGuid(), WaitingForUsername = "targetuser" };
        var room = CreateRoomWithAccesses(targetUserId);

        _roomService.Setup(s => s.GetWithGameAsync(It.IsAny<Guid>())).ReturnsAsync(room);
        _userLookupService.Setup(s => s.FindUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, targetUserId));
        _repository.Setup(r => r.Create(It.IsAny<CreatePostPendencyEntity>()))
            .ReturnsAsync(new PostPendency());

        await _service.CreateAsync(createPendency);

        _intentionManager.Verify(m => m.ThrowIfForbidden(RoomIntention.CreatePostPendency, room), Times.Once);
    }

    [Fact]
    public async Task PreventDuplicatePendencyForSameUser()
    {
        var waitingForUserId = Guid.NewGuid();
        var currentUserId = _identityProvider.Object.Current.User.UserId;
        var createPendency = new CreatePostPendency { RoomId = Guid.NewGuid(), WaitingForUsername = "targetuser" };

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
                new RoomAccess { Character = new Character { Author = new GeneralUser { UserId = waitingForUserId } } }
            }
        };

        _roomService.Setup(s => s.GetWithGameAsync(It.IsAny<Guid>())).ReturnsAsync(room);
        _userLookupService.Setup(s => s.FindUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, waitingForUserId));

        var act = async () => await _service.CreateAsync(createPendency);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreatePendencyAndPublishEvent()
    {
        var pendencyId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var createPendency = new CreatePostPendency { RoomId = Guid.NewGuid(), WaitingForUsername = "targetuser" };
        var room = CreateRoomWithAccesses(targetUserId);

        _roomService.Setup(s => s.GetWithGameAsync(It.IsAny<Guid>())).ReturnsAsync(room);
        _userLookupService.Setup(s => s.FindUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, targetUserId));
        _repository.Setup(r => r.Create(It.IsAny<CreatePostPendencyEntity>()))
            .ReturnsAsync(new PostPendency { Id = pendencyId });

        await _service.CreateAsync(createPendency);

        _repository.Verify(r => r.Create(It.IsAny<CreatePostPendencyEntity>()), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.RoomPendencyCreated, pendencyId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeDeletePostPendencyAction()
    {
        var pendencyId = Guid.NewGuid();
        var pendency = new PostPendency { Id = pendencyId };

        _repository.Setup(r => r.Get(pendencyId)).ReturnsAsync(pendency);
        _repository.Setup(r => r.Delete(pendencyId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(pendencyId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(RoomIntention.DeletePostPendency, pendency), Times.Once);
    }

    [Fact]
    public async Task DeletePendencyAndPublishEvent()
    {
        var pendencyId = Guid.NewGuid();
        var pendency = new PostPendency { Id = pendencyId };

        _repository.Setup(r => r.Get(pendencyId)).ReturnsAsync(pendency);
        _repository.Setup(r => r.Delete(pendencyId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(pendencyId);

        _repository.Verify(r => r.Delete(pendencyId), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.RoomPendencyDeleted, pendencyId), Times.Once);
    }

    /// <summary>
    /// The projection that carries the game, which is what the permission rule
    /// reads: asked with the flat one the manager finds no resolver and refuses
    /// everybody.
    /// </summary>
    private RoomToUpdate CreateRoomWithAccesses(Guid? userId = null)
    {
        return new RoomToUpdate
        {
            Game = new GameDto(),
            Pendencies = new List<PostPendency>(),
            Accesses = new List<RoomAccess>
            {
                new RoomAccess { Character = new Character { Author = new GeneralUser { UserId = userId ?? Guid.NewGuid() } } }
            }
        };
    }
}
