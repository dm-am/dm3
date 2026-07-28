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
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Core.Users;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class PostServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IRoomRepository> _roomRepository;
    private readonly Mock<IPostRepository> _repository;
    private readonly Mock<IDiceRollRepository> _diceRollRepository;
    private readonly Mock<IDiceRoller> _diceRoller;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly PostService _service;

    public PostServiceShould()
    {
        var createValidator = Mock<IValidator<CreatePost>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreatePost>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdatePost>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdatePost>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var roomService = Mock<IRoomService>();

        _roomRepository = Mock<IRoomRepository>();

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<RoomIntention>(), It.IsAny<object>()));
        _intentionManager.Setup(m => m.IsAllowed(It.IsAny<PostIntention>(), It.IsAny<object>())).Returns(true);
        _intentionManager.Setup(m => m.IsAllowed(It.IsAny<RoomIntention>(), It.IsAny<object>())).Returns(true);

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _repository = Mock<IPostRepository>();

        _diceRollRepository = Mock<IDiceRollRepository>();
        _diceRoller = Mock<IDiceRoller>();

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _producer.Setup(p => p.SendAsync(It.IsAny<IEnumerable<EventType>>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(Guid.NewGuid(), "testuser");
        _identityProvider.Setup(p => p.Current).Returns(identity);

        _service = new PostService(
            createValidator.Object,
            updateValidator.Object,
            roomService.Object,
            _roomRepository.Object,
            _intentionManager.Object,
            dateTimeProvider.Object,
            guidFactory.Object,
            _repository.Object,
            _diceRollRepository.Object,
            _diceRoller.Object,
            _unreadCountersRepository.Object,
            _producer.Object,
            _identityProvider.Object);
    }

    [Fact]
    public async Task AuthorizeCreatePostAction()
    {
        var roomId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, CharacterId = characterId, GameText = "Test post" };
        var room = new RoomToUpdate { Id = roomId, Pendencies = new List<PostPendency>(), Accesses = new List<RoomAccess>(), Game = new GameDto() };

        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreatePostEntity>())).ReturnsAsync(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await _service.CreateAsync(createPost);

        _intentionManager.Verify(m => m.ThrowIfForbidden(
            RoomIntention.CreatePost,
            It.Is<(RoomToUpdate, Guid?)>(t => t.Item1 == room && t.Item2 == characterId)),
            Times.Once);
    }

    [Fact]
    public async Task CreatePostAndIncrementUnreadCounters()
    {
        var roomId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, GameText = "Test post" };
        var room = new RoomToUpdate { Id = roomId, Pendencies = new List<PostPendency>(), Accesses = new List<RoomAccess>(), Game = new GameDto() };
        var createdPost = new Post { Id = Guid.NewGuid(), RoomId = roomId };

        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreatePostEntity>())).ReturnsAsync(createdPost);

        await _service.CreateAsync(createPost);

        _repository.Verify(r => r.Create(It.IsAny<CreatePostEntity>()), Times.Once);
        _unreadCountersRepository.Verify(r => r.IncrementAsync(roomId, UnreadEntryType.Message), Times.Once);
    }

    [Fact]
    public async Task PublishNewPostEvent()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, GameText = "Test post" };
        var room = new RoomToUpdate { Id = roomId, Pendencies = new List<PostPendency>(), Accesses = new List<RoomAccess>(), Game = new GameDto() };

        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreatePostEntity>())).ReturnsAsync(new Post { Id = postId, RoomId = roomId });

        await _service.CreateAsync(createPost);

        _producer.Verify(p => p.SendAsync(It.Is<IEnumerable<EventType>>(events => events.Contains(EventType.NewPost)), postId), Times.Once);
    }

    [Fact]
    public async Task RollAndPersistDiceWhenRoomDiceEnabled()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var createPost = new CreatePost
        {
            RoomId = roomId,
            GameText = "Test post",
            DiceRolls = new[] { new CreatePostDiceRoll { EdgesCount = 20, DiceCount = 1 } }
        };
        var room = new RoomToUpdate
        {
            Id = roomId,
            Pendencies = new List<PostPendency>(),
            Accesses = new List<RoomAccess>(),
            Game = new GameDto(),
            Settings = new RoomSettings { DiceEnabled = true }
        };
        var createdPost = new Post { Id = postId, RoomId = roomId };
        var rolledDice = new List<DiceRoll> { new() { Id = Guid.NewGuid(), PostId = postId, EdgesCount = 20 } };

        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreatePostEntity>())).ReturnsAsync(createdPost);
        _diceRoller
            .Setup(r => r.Roll(postId, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<CreatePostDiceRoll>>()))
            .Returns(rolledDice);

        var result = await _service.CreateAsync(createPost);

        _diceRoller.Verify(r => r.Roll(postId, It.IsAny<DateTimeOffset>(),
            It.Is<IEnumerable<CreatePostDiceRoll>>(s => s.Count() == 1)), Times.Once);
        _diceRollRepository.Verify(r => r.CreateAsync(rolledDice), Times.Once);
        result.DiceRolls.Should().BeSameAs(rolledDice);
    }

    [Fact]
    public async Task NotPersistDiceWhenRoomDiceDisabled()
    {
        var roomId = Guid.NewGuid();
        var createPost = new CreatePost
        {
            RoomId = roomId,
            GameText = "Test post",
            DiceRolls = new[] { new CreatePostDiceRoll { EdgesCount = 20, DiceCount = 1 } }
        };
        var room = new RoomToUpdate
        {
            Id = roomId,
            Pendencies = new List<PostPendency>(),
            Accesses = new List<RoomAccess>(),
            Game = new GameDto(),
            Settings = new RoomSettings { DiceEnabled = false }
        };

        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreatePostEntity>()))
            .ReturnsAsync(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await _service.CreateAsync(createPost);

        _diceRoller.Verify(r => r.Roll(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<IEnumerable<CreatePostDiceRoll>>()), Times.Never);
        _diceRollRepository.Verify(r => r.CreateAsync(It.IsAny<IEnumerable<DiceRoll>>()), Times.Never);
    }

    [Fact]
    public async Task AuthorizeDeletePostAction()
    {
        var postId = Guid.NewGuid();
        var post = new Post { Id = postId, RoomId = Guid.NewGuid(), Author = new GeneralUser { UserId = Guid.NewGuid() }, CreatedUtc = DateTimeOffset.UtcNow };

        _repository.Setup(r => r.Get(postId, It.IsAny<Guid>())).ReturnsAsync(post);
        _repository.Setup(r => r.Delete(postId)).Returns(Task.CompletedTask);
        _repository.Setup(r => r.DecrementAuthorQuantityRating(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        await _service.DeleteAsync(postId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(PostIntention.Delete, post), Times.Once);
    }

    [Fact]
    public async Task DeletePostAndDecrementCounters()
    {
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow;
        var post = new Post { Id = postId, RoomId = roomId, Author = new GeneralUser { UserId = authorId }, CreatedUtc = createdUtc };

        _repository.Setup(r => r.Get(postId, It.IsAny<Guid>())).ReturnsAsync(post);
        _repository.Setup(r => r.Delete(postId)).Returns(Task.CompletedTask);
        _repository.Setup(r => r.DecrementAuthorQuantityRating(authorId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(postId);

        _repository.Verify(r => r.Delete(postId), Times.Once);
        _repository.Verify(r => r.DecrementAuthorQuantityRating(authorId), Times.Once);
        _unreadCountersRepository.Verify(r => r.DecrementAsync(roomId, UnreadEntryType.Message, createdUtc), Times.Once);
    }
}
