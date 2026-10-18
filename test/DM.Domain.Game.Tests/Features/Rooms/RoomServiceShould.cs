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
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Core.Users;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Rooms;

public class RoomServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IGameService _gameService;
    private readonly IRoomRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IEventProducer _producer;
    private readonly IGuidFactory _guidFactory;
    private static readonly Guid CurrentUserId = Guid.NewGuid();

    private readonly IIdentityProvider _identityProvider;
    private readonly RoomService _service;

    public RoomServiceShould()
    {
        _gameService = Mock<IGameService>();

        var createValidator = Mock<IValidator<CreateRoom>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateRoom>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateRoom>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateRoom>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IRoomRepository>();

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(CurrentUserId, "testuser");
        _identityProvider.Current.Returns(identity);

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(Guid.NewGuid());

        _service = new RoomService(
            _gameService,
            createValidator,
            updateValidator,
            _intentionManager,
            _repository,
            _unreadCountersRepository,
            _producer,
            _identityProvider,
            _guidFactory);
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

        _gameService.GetAsync(gameId).Returns(game);
        _repository.Create(Arg.Any<CreateRoomEntity>()).Returns(new Room { Id = Guid.NewGuid(), GameId = gameId });

        await _service.CreateAsync(createRoom);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
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

        _gameService.GetAsync(gameId).Returns(game);
        _repository.Create(Arg.Any<CreateRoomEntity>()).Returns(new Room { Id = roomId, GameId = gameId });

        // The identifier is minted here and not learnt from the row: the marker is
        // written before the row exists to return one.
        _guidFactory.Create().Returns(roomId);

        await _service.CreateAsync(createRoom);

        await _unreadCountersRepository.Received(1).CreateMarkerAsync(roomId, gameId, UnreadEntryType.Message);
        // The row landed, so the reservation was committed.
        await _unreadCountersRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>());
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

        _gameService.GetAsync(gameId).Returns(game);
        _repository.Create(Arg.Any<CreateRoomEntity>()).Returns(new Room { Id = roomId, GameId = gameId });

        await _service.CreateAsync(createRoom);

        await _producer.Received(1).SendAsync(EventType.NewRoom, roomId);
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

        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await _service.UpdateAsync(updateRoom);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
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

        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await _service.UpdateAsync(updateRoom);

        await _repository.Received(1).Update(Arg.Is<UpdateRoomEntity>(e =>
            e.RoomId == roomId && e.Title == "Updated Room"));
        await _producer.Received(1).SendAsync(EventType.ChangedRoom, roomId);
    }

    /// <summary>
    /// Every patch field the caller sends reaches the repository.
    /// </summary>
    /// <remarks>
    /// The service copies the fields by hand and the suite asserted one of them,
    /// through Arg.Any on the rest: dropping Title stopped renaming a room and
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
        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.GetForUpdate(previousRoomId, Arg.Any<Guid>())
            .Returns(new RoomToUpdate { Id = previousRoomId, GameId = room.GameId });
        _repository.Update(Arg.Any<UpdateRoomEntity>())
            .Returns(room)
            .AndDoes(ci =>
            {
                var entity = ci.ArgAt<UpdateRoomEntity>(0);
                captured = entity;
            });

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

        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await _service.UpdateAsync(updateRoom);

        await _repository.Received(1).Update(Arg.Is<UpdateRoomEntity>(e => e.IsArchived == true));
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

        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await _service.UpdateAsync(updateRoom);

        await _repository.Received(1).Update(Arg.Is<UpdateRoomEntity>(e => e.IsArchived == null));
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

        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Delete(roomId, CurrentUserId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(roomId);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
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

        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Delete(roomId, CurrentUserId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(roomId);

        // With the author, because the schema carries a column for it and the moderation
        // screens read it: a room removed by a nameless somebody is the defect.
        await _repository.Received(1).Delete(roomId, CurrentUserId);
        await _unreadCountersRepository.Received(1).DeleteAsync(roomId, UnreadEntryType.Message);
        await _producer.Received(1).SendAsync(EventType.DeletedRoom, roomId);
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
        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _intentionManager.IsAllowed(GameIntention.SetStatusModeration).Returns(mayJudge);
        _repository
            .GetAllVisible(gameId, CurrentUserId, Arg.Any<bool>()).Returns(Array.Empty<Room>());

        await _service.GetAllAsync(gameId);

        await _repository.Received(1).GetAllVisible(gameId, CurrentUserId, mayJudge);
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
        _intentionManager.IsAllowed(GameIntention.SetStatusModeration).Returns(mayJudge);
        _repository
            .GetAvailable(roomId, CurrentUserId, Arg.Any<bool>()).Returns(new Room { Id = roomId });
        _unreadCountersRepository
            .SelectByEntitiesAsync(CurrentUserId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [roomId] = 0 });

        await _service.GetAsync(roomId);

        await _repository.Received(1).GetAvailable(roomId, CurrentUserId, mayJudge);
    }

    /// <summary>
    /// A room is moved after another room of its own game and nowhere else: an
    /// id of another game, of no room, or of the room itself is refused by name
    /// before anything is written.
    /// </summary>
    [Theory]
    [InlineData("another game")]
    [InlineData("no room")]
    [InlineData("itself")]
    public async Task RefuseToPutARoomAfterAnythingButARoomOfItsGame(string previous)
    {
        var roomId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var otherRoomId = Guid.NewGuid();
        var room = new RoomToUpdate
        {
            Id = roomId,
            GameId = gameId,
            Game = new GameDto { Id = gameId, Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" } }
        };
        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.GetForUpdate(otherRoomId, Arg.Any<Guid>()).Returns(previous == "another game"
            ? new RoomToUpdate { Id = otherRoomId, GameId = Guid.NewGuid() }
            : null);
        var target = previous == "itself" ? roomId : otherRoomId;

        var act = () => _service.UpdateAsync(new UpdateRoom
        {
            RoomId = roomId,
            PreviousRoomId = Optional<Guid>.WithValue(target)
        });

        (await act.Should().ThrowAsync<HttpBadRequestException>())
            .Which.ValidationErrors.Should().ContainKey("previousRoomId");
        await _repository.DidNotReceive().Update(Arg.Any<UpdateRoomEntity>());
    }

    /// <summary>
    /// The head of the list is asked for with an explicit null, and needs no
    /// room to stand after.
    /// </summary>
    [Fact]
    public async Task PutARoomFirstWithoutAskingForAnotherRoom()
    {
        var roomId = Guid.NewGuid();
        var room = new RoomToUpdate
        {
            Id = roomId,
            Game = new GameDto { Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" } }
        };
        _repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        _repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await _service.UpdateAsync(new UpdateRoom { RoomId = roomId, PreviousRoomId = Optional<Guid>.WithValue(null) });

        await _repository.Received(1).Update(Arg.Is<UpdateRoomEntity>(
            u => u.ShouldReorder && u.NewPreviousRoomId == null));
    }

    /// <summary>
    /// A closed room the reader may not open is named in the menu with the
    /// characters who write in it, as the old site writes it: "[Лес] - Густав,
    /// Эодор". Characters let in to read, people let in by name and a removed
    /// character are not among them, and nothing else of the room is told.
    /// </summary>
    [Fact]
    public async Task NameOnlyTheWritingCharactersOfARoomTheReaderMayNotOpen()
    {
        var gameId = Guid.NewGuid();
        var joined = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var closed = new Room
        {
            Id = Guid.NewGuid(),
            CanView = false,
            TotalPostsCount = 12,
            Accesses =
            [
                Grant(RoomAccessTargetType.Character, RoomAccessPolicy.Full, "Эодор", joined.AddDays(2)),
                Grant(RoomAccessTargetType.Character, RoomAccessPolicy.ReadOnly, "Читающий", joined),
                Grant(RoomAccessTargetType.Reader, RoomAccessPolicy.ReadOnly, null, null),
                Grant(RoomAccessTargetType.Character, RoomAccessPolicy.Full, null, null),
                Grant(RoomAccessTargetType.Character, RoomAccessPolicy.Full, "Густав", joined.AddDays(1))
            ]
        };
        var open = new Room
        {
            Id = Guid.NewGuid(),
            CanView = true,
            Accesses = [Grant(RoomAccessTargetType.Character, RoomAccessPolicy.Full, "Густав", joined)]
        };
        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _repository.GetAllVisible(gameId, CurrentUserId, Arg.Any<bool>()).Returns(new[] { closed, open });
        _unreadCountersRepository
            .SelectByEntitiesAsync(CurrentUserId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [closed.Id] = 3, [open.Id] = 0 });

        var rooms = (await _service.GetAllAsync(gameId)).ToArray();

        rooms[0].CharacterNames.Should().Equal("Густав", "Эодор");
        rooms[0].Accesses.Should().BeEmpty();
        rooms[0].TotalPostsCount.Should().Be(0);
        rooms[0].UnreadPostsCount.Should().Be(0);
        rooms[1].CharacterNames.Should().BeEmpty("the reader of a room is told by its accesses");
        rooms[1].Accesses.Should().ContainSingle();
    }

    private static RoomAccess Grant(
        RoomAccessTargetType target, RoomAccessPolicy policy, string? name, DateTimeOffset? joined) => new()
        {
            Id = Guid.NewGuid(),
            TargetType = target,
            Policy = policy,
            Character = name == null ? null! : new Character { Id = Guid.NewGuid(), Name = name, CreatedUtc = joined!.Value },
            User = new GeneralUser { UserId = Guid.NewGuid(), Username = "reader" }
        };
}
