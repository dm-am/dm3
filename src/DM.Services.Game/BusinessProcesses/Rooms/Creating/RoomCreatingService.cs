using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;

namespace DM.Services.Game.BusinessProcesses.Rooms.Creating;

/// <inheritdoc />
internal class RoomCreatingService : IRoomCreatingService
{
    private readonly IGameReadingService _gameReadingService;
    private readonly IValidator<CreateRoom> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IRoomFactory _roomFactory;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IRoomCreatingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public RoomCreatingService(
        IGameReadingService gameReadingService,
        IValidator<CreateRoom> validator,
        IIntentionManager intentionManager,
        IRoomFactory roomFactory,
        IUpdateBuilderFactory updateBuilderFactory,
        IRoomCreatingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer producer)
    {
        _gameReadingService = gameReadingService;
        _validator = validator;
        _intentionManager = intentionManager;
        _roomFactory = roomFactory;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task<Room> Create(CreateRoom createRoom)
    {
        await _validator.ValidateAndThrowAsync(createRoom);
        var game = await _gameReadingService.GetGame(createRoom.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        var lastRoom = await _repository.GetLastRoomInfo(createRoom.GameId);

        var roomToCreate = lastRoom == null
            ? _roomFactory.CreateFirst(createRoom)
            : _roomFactory.CreateAfter(createRoom, lastRoom.Id, lastRoom.OrderNumber);
        IUpdateBuilder<DbRoom>? updateLastRoom = lastRoom == null
            ? null
            : _updateBuilderFactory.Create<DbRoom>(lastRoom.Id).Field(r => r.NextRoomId, roomToCreate.RoomId);

        var room = await _repository.Create(roomToCreate, updateLastRoom);
        await _unreadCountersRepository.Create(room.Id, game.Id, UnreadEntryType.Message);
        await _producer.Send(EventType.NewRoom, room.Id);

        return room;
    }
}