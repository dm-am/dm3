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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class PostServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IRoomRepository _roomRepository;
    private readonly IPostRepository _repository;
    private readonly IDiceRollRepository _diceRollRepository;
    private readonly IDiceRoller _diceRoller;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
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
            .ValidateAsync(Arg.Any<ValidationContext<CreatePost>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdatePost>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdatePost>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var roomService = Mock<IRoomService>();

        _roomRepository = Mock<IRoomRepository>();

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.IsAllowed(Arg.Any<PostIntention>(), Arg.Any<object>()).Returns(true);
        _intentionManager.IsAllowed(Arg.Any<RoomIntention>(), Arg.Any<object>()).Returns(true);

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(_postId);

        _repository = Mock<IPostRepository>();

        _diceRollRepository = Mock<IDiceRollRepository>();
        _diceRoller = Mock<IDiceRoller>();

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);
        _producer.SendAsync(Arg.Any<IEnumerable<EventType>>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(_currentUserId, "testuser");
        _identityProvider.Current.Returns(identity);

        _service = new PostService(
            createValidator,
            updateValidator,
            roomService,
            _roomRepository,
            _intentionManager,
            dateTimeProvider,
            guidFactory,
            _repository,
            _diceRollRepository,
            _diceRoller,
            _unreadCountersRepository,
            _producer,
            _identityProvider);
    }

    [Fact]
    public async Task AuthorizeCreatePostAction()
    {
        var roomId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, CharacterId = characterId, GameText = "Test post" };
        var room = new RoomToUpdate { Id = roomId, Pendencies = new List<PostPendency>(), Accesses = new List<RoomAccess>(), Game = new GameDto() };

        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostEntity>()).Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await _service.CreateAsync(createPost);

        _intentionManager.Received(1).ThrowIfForbidden(
            RoomIntention.CreatePost,
            Arg.Is<(RoomToUpdate, Guid?)>(t => t.Item1 == room && t.Item2 == characterId));
    }

    [Fact]
    public async Task CreatePostAndIncrementUnreadCounters()
    {
        var roomId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, GameText = "Test post" };
        var room = new RoomToUpdate { Id = roomId, Pendencies = new List<PostPendency>(), Accesses = new List<RoomAccess>(), Game = new GameDto() };
        var createdPost = new Post { Id = Guid.NewGuid(), RoomId = roomId };

        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostEntity>()).Returns(createdPost);

        await _service.CreateAsync(createPost);

        await _repository.Received(1).Create(Arg.Any<CreatePostEntity>());
        await _unreadCountersRepository.Received(1).IncrementAsync(roomId, UnreadEntryType.Message);
    }

    [Fact]
    public async Task PublishNewPostEvent()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, GameText = "Test post" };
        var room = new RoomToUpdate { Id = roomId, Pendencies = new List<PostPendency>(), Accesses = new List<RoomAccess>(), Game = new GameDto() };

        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostEntity>()).Returns(new Post { Id = postId, RoomId = roomId });

        await _service.CreateAsync(createPost);

        await _producer.Received(1).SendAsync(Arg.Is<IEnumerable<EventType>>(events => events.Contains(EventType.NewPost)), postId);
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

        CreatePostEntity? written = null;
        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostEntity>())
            .Returns(createdPost)
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreatePostEntity>(0);
                written = e;
            });
        _diceRoller
            .Roll(postId, Arg.Any<DateTimeOffset>(), Arg.Any<IEnumerable<CreatePostDiceRoll>>()).Returns(rolledDice);

        var result = await _service.CreateAsync(createPost);

        _diceRoller.Received(1).Roll(postId, Arg.Any<DateTimeOffset>(),
            Arg.Is<IEnumerable<CreatePostDiceRoll>>(s => s.Count() == 1));
        // The rolls travel inside the entity: the repository writes them in the
        // post's own transaction, so both land or neither does (INV-6).
        written.Should().NotBeNull();
        written!.DiceRolls.Should().BeSameAs(rolledDice);
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

        CreatePostEntity? written = null;
        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostEntity>())
            .Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreatePostEntity>(0);
                written = e;
            });

        await _service.CreateAsync(createPost);

        _diceRoller.DidNotReceive().Roll(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<IEnumerable<CreatePostDiceRoll>>());
        written.Should().NotBeNull();
        written!.DiceRolls.Should().BeEmpty("dice are dropped when the room has rolling disabled");
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
        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostEntity>())
            .Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreatePostEntity>(0);
                written = e;
            });

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
        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Create(Arg.Any<CreatePostEntity>())
            .Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreatePostEntity>(0);
                written = e;
            });

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
                    new[] { new PrivateAddressee(Guid.NewGuid(), "Анна", annaOwner) })
        };
        // Anna has left the room by the time the post is edited; Boris has not.
        var room = RoomWith(roomId, ("Борис", borisOwner));

        UpdatePostEntity? written = null;
        _repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Update(Arg.Any<UpdatePostEntity>())
            .Returns(post)
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdatePostEntity>(0);
                written = e;
            });

        await _service.UpdateAsync(new UpdatePost
        {
            PostId = postId,
            GameText = "[private=Анна]только Анне[/private] и [private=Борис]Борису[/private]"
        });

        var snapshot = PrivateAddresseeSnapshot.Parse(written!.PrivateAddresseeSnapshotJson);
        snapshot.Should().ContainKey("Анна").WhoseValue.Should().BeEquivalentTo(new[] { annaOwner });
        snapshot.Should().ContainKey("Борис").WhoseValue.Should().BeEquivalentTo(new[] { borisOwner });
    }

    /// <summary>
    /// An edit whose [private] name has changed hands since the post was saved
    /// is refused, and refused before anything is written: the block would go to
    /// the character the name used to mean and miss the one it means now, and
    /// which of the two the author meant is not readable off the text.
    /// </summary>
    [Fact]
    public async Task RefuseAnEditWhosePrivateNameNowMeansAnotherCharacter()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var chuck = Guid.NewGuid();
        var annaOwner = Guid.NewGuid();
        var borisOwner = Guid.NewGuid();
        const string text = "[private=Чак]секрет[/private]";
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            GameText = text,
            PrivateAddresseeSnapshotJson = PrivateAddresseeSnapshot.Build(
                text, new[] { new PrivateAddressee(chuck, "Чак", annaOwner) })
        };
        // The character the block was frozen to has been renamed, and the name it
        // gave up now belongs to somebody else's character in the same room.
        var room = RoomWithCharacters(roomId,
            (chuck, "Владимир", annaOwner),
            (Guid.NewGuid(), "Чак", borisOwner));

        _repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);

        var act = () => _service.UpdateAsync(new UpdatePost { PostId = postId, GameText = text });

        await act.Should().ThrowAsync<HttpException>()
            .WithMessage(PrivateAddresseeSnapshot.DescribeRenameRefusal("Чак"));
        await _repository.DidNotReceive().Update(Arg.Any<UpdatePostEntity>());
    }

    /// <summary>A room whose access list holds the given characters.</summary>
    private static RoomToUpdate RoomWith(Guid roomId, params (string Name, Guid OwnerId)[] characters) =>
        RoomWithCharacters(roomId, characters
            .Select(c => (Guid.NewGuid(), c.Name, c.OwnerId))
            .ToArray());

    /// <summary>The same, when the test has to say which character is which.</summary>
    private static RoomToUpdate RoomWithCharacters(
        Guid roomId, params (Guid CharacterId, string Name, Guid OwnerId)[] characters)
    {
        var accesses = new List<RoomAccess>();
        foreach (var (characterId, name, ownerId) in characters)
        {
            accesses.Add(new RoomAccess
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TargetType = RoomAccessTargetType.Character,
                Character = new Character
                {
                    Id = characterId,
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

        _repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        _repository.Delete(postId, _currentUserId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(postId);

        _intentionManager.Received(1).ThrowIfForbidden(PostIntention.Delete, post);
    }

    [Fact]
    public async Task DeletePostAndDecrementUnreadCounters()
    {
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow;
        var post = new Post { Id = postId, RoomId = roomId, Author = new GeneralUser { UserId = authorId }, CreatedUtc = createdUtc };

        _repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        _repository.Delete(postId, _currentUserId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(postId);

        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        await _repository.Received(1).Delete(postId, _currentUserId);
        await _unreadCountersRepository.Received(1).DecrementAsync(roomId, UnreadEntryType.Message, createdUtc);
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

        _repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(RoomWith(roomId));
        _repository.Update(Arg.Any<UpdatePostEntity>()).Returns(post);
        _intentionManager
            .When(m => m.ThrowIfForbidden(PostIntention.EditText, Arg.Any<object>()))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => _service.UpdateAsync(new UpdatePost
        {
            PostId = postId,
            GameText = "как стало"
        });

        await act.Should().ThrowAsync<HttpException>();
        // A refused edit writes nothing.
        await _repository.DidNotReceive().Update(Arg.Any<UpdatePostEntity>());
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

        _repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        _roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(RoomWith(roomId));
        _repository.Update(Arg.Any<UpdatePostEntity>()).Returns(post);

        await _service.UpdateAsync(new UpdatePost { PostId = postId });

        _intentionManager.DidNotReceive().ThrowIfForbidden(PostIntention.EditText, Arg.Any<object>());
    }
}
