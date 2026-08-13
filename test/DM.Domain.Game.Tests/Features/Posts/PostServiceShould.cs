using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
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
    private readonly Guid _currentUserId = Guid.NewGuid();

    /// <summary>
    /// The id the factory hands the service. The post id is generated before the
    /// write rather than read back from it — that is what lets the dice be stored
    /// first — so a test that wants to talk about the post has to know it up front.
    /// </summary>
    private readonly Guid _postId = Guid.NewGuid();
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
        guidFactory.Setup(g => g.Create()).Returns(_postId);

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
        var identity = Identities.User(_currentUserId, "testuser");
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
        var postId = _postId;
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

    /// <summary>
    /// The dice go into Mongo before the post goes into PostgreSQL, and come back
    /// out if the post does not follow.
    /// </summary>
    /// <remarks>
    /// There is no transaction across the two stores, so the order is the whole
    /// guarantee. A roll cannot be produced a second time — rolling again answers
    /// a different number — so of the two possible losses only the post is
    /// recoverable: the author simply posts again.
    /// </remarks>
    [Fact]
    public async Task DropTheDiceWhenThePostDoesNotFollowThem()
    {
        var roomId = Guid.NewGuid();
        var postId = _postId;
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
        var rolledDice = new List<DiceRoll> { new() { Id = Guid.NewGuid(), PostId = postId, EdgesCount = 20 } };

        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _diceRoller
            .Setup(r => r.Roll(postId, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<CreatePostDiceRoll>>()))
            .Returns(rolledDice);
        _repository.Setup(r => r.Create(It.IsAny<CreatePostEntity>()))
            .ThrowsAsync(new InvalidOperationException("insert failed"));

        var act = async () => await _service.CreateAsync(createPost);

        // The failure still reaches the caller: what is compensated is the
        // remainder in the other store, not the error.
        await act.Should().ThrowAsync<InvalidOperationException>();
        _diceRollRepository.Verify(r => r.CreateAsync(rolledDice), Times.Once);
        _diceRollRepository.Verify(r => r.DeleteByPostIdAsync(postId), Times.Once);
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

    /// <summary>
    /// The addressee rule is one of five in BBCODE_RENDERING.md, and it reads a
    /// field the save path used to leave at its default — so the player a line
    /// was addressed to was the one reader who could not read it.
    /// </summary>
    [Fact]
    public async Task ResolvePrivateAddresseesOfANewPost()
    {
        var roomId = Guid.NewGuid();
        var annaOwner = Guid.NewGuid();
        var room = RoomWith(roomId, ("Анна", annaOwner), ("Борис", Guid.NewGuid()));
        var createPost = new CreatePost
        {
            RoomId = roomId,
            GameText = "всем [private=Анна]только Анне[/private]"
        };

        CreatePostEntity? written = null;
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreatePostEntity>()))
            .Callback<CreatePostEntity>(e => written = e)
            .ReturnsAsync(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await _service.CreateAsync(createPost);

        written.Should().NotBeNull();
        PrivateAddresseeSnapshot.Parse(written!.PrivateAddresseeSnapshotJson)
            .Should().ContainKey("Анна")
            .WhoseValue.Should().BeEquivalentTo(new[] { annaOwner });
    }

    [Fact]
    public async Task ResolveNoPrivateAddresseeForANameOutsideTheRoom()
    {
        var roomId = Guid.NewGuid();
        var room = RoomWith(roomId, ("Анна", Guid.NewGuid()));
        var createPost = new CreatePost
        {
            RoomId = roomId,
            GameText = "[private=Виктор]секрет[/private]"
        };

        CreatePostEntity? written = null;
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Create(It.IsAny<CreatePostEntity>()))
            .Callback<CreatePostEntity>(e => written = e)
            .ReturnsAsync(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await _service.CreateAsync(createPost);

        written!.PrivateAddresseeSnapshotJson.Should().Be(PrivateAddresseeSnapshot.Empty);
    }

    /// <summary>
    /// Addressee-forever survives an edit: a block resolved by the first save
    /// keeps its owner even after the character loses access to the room, while
    /// a block the edit introduced resolves against the roster it is saved with.
    /// </summary>
    [Fact]
    public async Task FreezeResolvedAddresseesAcrossAnEdit()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var annaOwner = Guid.NewGuid();
        var borisOwner = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            GameText = "[private=Анна]только Анне[/private]",
            PrivateAddresseeSnapshotJson =
                PrivateAddresseeSnapshot.Build(
                    "[private=Анна]только Анне[/private]",
                    new[] { new PrivateAddressee("Анна", annaOwner) })
        };
        // Anna has left the room by the time the post is edited; Boris has not.
        var room = RoomWith(roomId, ("Борис", borisOwner));

        UpdatePostEntity? written = null;
        _repository.Setup(r => r.Get(postId, It.IsAny<Guid>())).ReturnsAsync(post);
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(room);
        _repository.Setup(r => r.Update(It.IsAny<UpdatePostEntity>()))
            .Callback<UpdatePostEntity>(e => written = e)
            .ReturnsAsync(post);

        await _service.UpdateAsync(new UpdatePost
        {
            PostId = postId,
            GameText = "[private=Анна]только Анне[/private] и [private=Борис]Борису[/private]"
        });

        var snapshot = PrivateAddresseeSnapshot.Parse(written!.PrivateAddresseeSnapshotJson);
        snapshot.Should().ContainKey("Анна").WhoseValue.Should().BeEquivalentTo(new[] { annaOwner });
        snapshot.Should().ContainKey("Борис").WhoseValue.Should().BeEquivalentTo(new[] { borisOwner });
    }

    /// <summary>A room whose access list holds the given characters.</summary>
    private static RoomToUpdate RoomWith(Guid roomId, params (string Name, Guid OwnerId)[] characters)
    {
        var accesses = new List<RoomAccess>();
        foreach (var (name, ownerId) in characters)
        {
            accesses.Add(new RoomAccess
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TargetType = RoomAccessTargetType.Character,
                Character = new Character
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Author = new GeneralUser { UserId = ownerId }
                }
            });
        }

        return new RoomToUpdate
        {
            Id = roomId,
            Pendencies = new List<PostPendency>(),
            Accesses = accesses,
            Game = new GameDto()
        };
    }

    [Fact]
    public async Task AuthorizeDeletePostAction()
    {
        var postId = Guid.NewGuid();
        var post = new Post { Id = postId, RoomId = Guid.NewGuid(), Author = new GeneralUser { UserId = Guid.NewGuid() }, CreatedUtc = DateTimeOffset.UtcNow };

        _repository.Setup(r => r.Get(postId, It.IsAny<Guid>())).ReturnsAsync(post);
        _repository.Setup(r => r.Delete(postId, _currentUserId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(postId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(PostIntention.Delete, post), Times.Once);
    }

    [Fact]
    public async Task DeletePostAndDecrementUnreadCounters()
    {
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow;
        var post = new Post { Id = postId, RoomId = roomId, Author = new GeneralUser { UserId = authorId }, CreatedUtc = createdUtc };

        _repository.Setup(r => r.Get(postId, It.IsAny<Guid>())).ReturnsAsync(post);
        _repository.Setup(r => r.Delete(postId, _currentUserId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(postId);

        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        _repository.Verify(r => r.Delete(postId, _currentUserId), Times.Once);
        _unreadCountersRepository.Verify(r => r.DecrementAsync(roomId, UnreadEntryType.Message, createdUtc), Times.Once);
    }

    /// <summary>
    /// Submitted text the caller may not edit is refused, not swapped for the
    /// stored text and saved. The refusal used to come back 200 with the post
    /// exactly as it was, which is what a successful edit looks like, so a client
    /// could not tell an edit that was denied from one that changed nothing.
    /// </summary>
    [Fact]
    public async Task RefuseTextTheCallerMayNotEdit()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            GameText = "как было"
        };

        _repository.Setup(r => r.Get(postId, It.IsAny<Guid>())).ReturnsAsync(post);
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(RoomWith(roomId));
        _repository.Setup(r => r.Update(It.IsAny<UpdatePostEntity>())).ReturnsAsync(post);
        _intentionManager
            .Setup(m => m.ThrowIfForbidden(PostIntention.EditText, It.IsAny<object>()))
            .Throws(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => _service.UpdateAsync(new UpdatePost
        {
            PostId = postId,
            GameText = "как стало"
        });

        await act.Should().ThrowAsync<HttpException>();
        _repository.Verify(r => r.Update(It.IsAny<UpdatePostEntity>()), Times.Never,
            "a refused edit writes nothing");
    }

    /// <summary>
    /// A request that submits no text asks for nothing: the lead who may change
    /// only the character must not be refused for the text he never sent.
    /// </summary>
    [Fact]
    public async Task NotAskForTextRightsWhenNoTextIsSubmitted()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            GameText = "как было"
        };

        _repository.Setup(r => r.Get(postId, It.IsAny<Guid>())).ReturnsAsync(post);
        _roomRepository.Setup(r => r.GetForUpdate(roomId, It.IsAny<Guid>())).ReturnsAsync(RoomWith(roomId));
        _repository.Setup(r => r.Update(It.IsAny<UpdatePostEntity>())).ReturnsAsync(post);

        await _service.UpdateAsync(new UpdatePost { PostId = postId });

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(PostIntention.EditText, It.IsAny<object>()), Times.Never);
    }
}
