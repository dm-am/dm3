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
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Tests.Dsl;
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
    private static readonly Guid CurrentUserId = Guid.NewGuid();

    private readonly IIntentionManager intentionManager;
    private readonly IGameService gameService;
    private readonly IRoomRepository repository;
    private readonly IUnreadCountersRepository unreadCountersRepository;
    private readonly IEventProducer producer;
    private readonly IGuidFactory guidFactory;
    private readonly IIdentityProvider identityProvider;
    private readonly RoomService service;

    public RoomServiceShould()
    {
        gameService = Mock<IGameService>();

        var createValidator = Mock<IValidator<CreateRoom>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateRoom>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateRoom>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateRoom>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        intentionManager = Mock<IIntentionManager>();

        repository = Mock<IRoomRepository>();

        unreadCountersRepository = Mock<IUnreadCountersRepository>();

        producer = Mock<IEventProducer>();
        producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(CurrentUserId, "testuser");
        identityProvider.Current.Returns(identity);

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        service = new RoomService(
            gameService,
            createValidator,
            updateValidator,
            intentionManager,
            repository,
            unreadCountersRepository,
            producer,
            identityProvider,
            guidFactory);
    }

    [Fact]
    public async Task AuthorizeCreateRoomAction()
    {
        var gameId = Guid.NewGuid();
        var createRoom = new CreateRoom { GameId = gameId, Title = "Test Room" };
        var game = GameBuilder.Bare(gameId);

        gameService.GetAsync(gameId).Returns(game);
        repository.Create(Arg.Any<CreateRoomEntity>()).Returns(new Room { Id = Guid.NewGuid(), GameId = gameId });

        await service.CreateAsync(createRoom);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task CreateRoomAndInitializeUnreadCounters()
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createRoom = new CreateRoom { GameId = gameId, Title = "Test Room" };
        var game = GameBuilder.Bare(gameId);

        gameService.GetAsync(gameId).Returns(game);
        repository.Create(Arg.Any<CreateRoomEntity>()).Returns(new Room { Id = roomId, GameId = gameId });

        // The identifier is minted here and not learnt from the row: the marker is
        // written before the row exists to return one.
        guidFactory.Create().Returns(roomId);

        await service.CreateAsync(createRoom);

        await unreadCountersRepository.Received(1).CreateMarkerAsync(roomId, gameId, UnreadEntryType.Message);
        // The row landed, so the reservation was committed.
        await unreadCountersRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>());
    }

    [Fact]
    public async Task PublishNewRoomEvent()
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createRoom = new CreateRoom { GameId = gameId, Title = "Test Room" };
        var game = GameBuilder.Bare(gameId);

        gameService.GetAsync(gameId).Returns(game);
        repository.Create(Arg.Any<CreateRoomEntity>()).Returns(new Room { Id = roomId, GameId = gameId });

        await service.CreateAsync(createRoom);

        await producer.Received(1).SendAsync(EventType.NewRoom, roomId);
    }

    [Fact]
    public async Task AuthorizeUpdateRoomAction()
    {
        var roomId = Guid.NewGuid();
        var updateRoom = new UpdateRoom { RoomId = roomId, Title = "Updated Room" };
        var game = GameBuilder.Bare();
        var room = new RoomToUpdate { Id = roomId, Game = game };

        repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await service.UpdateAsync(updateRoom);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task UpdateRoomAndPublishEvent()
    {
        var roomId = Guid.NewGuid();
        var updateRoom = new UpdateRoom { RoomId = roomId, Title = "Updated Room" };
        var room = RoomOf(roomId);

        repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await service.UpdateAsync(updateRoom);

        await repository.Received(1).Update(Arg.Is<UpdateRoomEntity>(e =>
            e.RoomId == roomId && e.Title == "Updated Room"));
        await producer.Received(1).SendAsync(EventType.ChangedRoom, roomId);
    }

    // The service copies the fields by hand, so the captured entity is compared field by field.
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
            ChatId = chatId
        };
        var room = RoomOf(roomId);

        UpdateRoomEntity? captured = null;
        repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.GetForUpdate(previousRoomId, Arg.Any<Guid>())
            .Returns(new RoomToUpdate { Id = previousRoomId, GameId = room.GameId });
        repository.Update(Arg.Do<UpdateRoomEntity>(entity => captured = entity))
            .Returns(room);

        await service.UpdateAsync(updateRoom);

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
    }

    [Fact]
    public async Task LeaveIsArchivedUnsetWhenUpdateOmitsIt()
    {
        var roomId = Guid.NewGuid();
        var updateRoom = new UpdateRoom { RoomId = roomId, Title = "Updated Room" };
        var room = RoomOf(roomId);

        repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await service.UpdateAsync(updateRoom);

        await repository.Received(1).Update(Arg.Is<UpdateRoomEntity>(e => e.IsArchived == null));
    }

    [Fact]
    public async Task AuthorizeDeleteRoomAction()
    {
        var roomId = Guid.NewGuid();
        var game = GameBuilder.Bare();
        var room = new RoomToUpdate { Id = roomId, Game = game };

        repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Delete(roomId, CurrentUserId).Returns(Task.CompletedTask);

        await service.DeleteAsync(roomId);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task DeleteRoomAndCleanupUnreadCounters()
    {
        var roomId = Guid.NewGuid();
        var room = RoomOf(roomId);

        repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Delete(roomId, CurrentUserId).Returns(Task.CompletedTask);

        await service.DeleteAsync(roomId);

        // With the author, because the schema carries a column for it and the moderation
        // screens read it: a room removed by a nameless somebody is the defect.
        await repository.Received(1).Delete(roomId, CurrentUserId);
        await unreadCountersRepository.Received(1).DeleteAsync(roomId, UnreadEntryType.Message);
        await producer.Received(1).SendAsync(EventType.DeletedRoom, roomId);
    }

    /// <summary>
    /// The rank that opens a premoderated game reaches the storage scope of its
    /// rooms, and it is the intention that answers it — not a second comparison of
    /// roles inside the service.
    /// </summary>
    /// <remarks>
    /// Both directions, because the parameter defaults to false.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScopeTheRoomListByThePremoderationVerdictIntention(bool mayJudge)
    {
        var gameId = Guid.NewGuid();
        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        intentionManager.IsAllowed(GameIntention.SetStatusModeration).Returns(mayJudge);
        repository
            .GetAllVisible(gameId, CurrentUserId, Arg.Any<bool>()).Returns(Array.Empty<Room>());

        await service.GetAllAsync(gameId);

        await repository.Received(1).GetAllVisible(gameId, CurrentUserId, mayJudge);
    }

    // The same rank on the point read, or the menu would name rooms whose own address then answered
    // 404.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScopeASingleRoomByThePremoderationVerdictIntention(bool mayJudge)
    {
        var roomId = Guid.NewGuid();
        intentionManager.IsAllowed(GameIntention.SetStatusModeration).Returns(mayJudge);
        repository
            .GetAvailable(roomId, CurrentUserId, Arg.Any<bool>()).Returns(new Room { Id = roomId });
        unreadCountersRepository
            .SelectByEntitiesAsync(CurrentUserId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [roomId] = 0 });

        await service.GetAsync(roomId);

        await repository.Received(1).GetAvailable(roomId, CurrentUserId, mayJudge);
    }

    // A room is moved after another room of its own game and nowhere else: an id of another game,
    // of no room, or of the room itself is refused by name before anything is written.
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
            Game = GameBuilder.Bare(gameId)
        };
        repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.GetForUpdate(otherRoomId, Arg.Any<Guid>()).Returns(previous == "another game"
            ? new RoomToUpdate { Id = otherRoomId, GameId = Guid.NewGuid() }
            : null);
        var target = previous == "itself" ? roomId : otherRoomId;

        var act = () => service.UpdateAsync(new UpdateRoom
        {
            RoomId = roomId,
            PreviousRoomId = Optional<Guid>.WithValue(target)
        });

        (await act.Should().ThrowAsync<HttpBadRequestException>())
            .Which.ValidationErrors.Should().ContainKey("previousRoomId");
        await repository.DidNotReceive().Update(Arg.Any<UpdateRoomEntity>());
    }

    // The head of the list is asked for with an explicit null, and needs no room to stand after.
    [Fact]
    public async Task PutARoomFirstWithoutAskingForAnotherRoom()
    {
        var roomId = Guid.NewGuid();
        var room = RoomOf(roomId);
        repository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Update(Arg.Any<UpdateRoomEntity>()).Returns(room);

        await service.UpdateAsync(new UpdateRoom { RoomId = roomId, PreviousRoomId = Optional<Guid>.WithValue(null) });

        await repository.Received(1).Update(Arg.Is<UpdateRoomEntity>(
            u => u.ShouldReorder && u.NewPreviousRoomId == null));
    }

    // A closed room the reader may not open is named in the menu with the characters who write in
    // it, as the old site writes it: "[Лес] - Густав, Эодор". Characters let in to read, people let
    // in by name and a removed character are not among them, and nothing else of the room is told.
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
        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        repository.GetAllVisible(gameId, CurrentUserId, Arg.Any<bool>()).Returns(new[] { closed, open });
        unreadCountersRepository
            .SelectByEntitiesAsync(CurrentUserId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [closed.Id] = 3, [open.Id] = 0 });

        var rooms = (await service.GetAllAsync(gameId)).ToArray();

        rooms[0].CharacterNames.Should().Equal("Густав", "Эодор");
        rooms[0].Accesses.Should().BeEmpty();
        rooms[0].TotalPostsCount.Should().Be(0);
        rooms[0].UnreadPostsCount.Should().Be(0);
        rooms[1].CharacterNames.Should().BeEmpty("the reader of a room is told by its accesses");
        rooms[1].Accesses.Should().ContainSingle();
    }

    private static RoomToUpdate RoomOf(Guid roomId) => new() { Id = roomId, Game = GameBuilder.Bare() };

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
