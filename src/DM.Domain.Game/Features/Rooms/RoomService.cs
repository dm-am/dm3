using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Game.Features.Rooms;

/// <summary>
/// Unified service for room CRUD operations
/// </summary>
internal class RoomService : IRoomService
{
    private readonly IGameService _gameService;
    private readonly IValidator<CreateRoom> _createValidator;
    private readonly IValidator<UpdateRoom> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IRoomRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;

    public RoomService(
        IGameService gameService,
        IValidator<CreateRoom> createValidator,
        IValidator<UpdateRoom> updateValidator,
        IIntentionManager intentionManager,
        IRoomRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IEventProducer producer,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory)
    {
        _gameService = gameService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
    }

    #region Create

    public async Task<Room> CreateAsync(CreateRoom createRoom)
    {
        await _createValidator.ValidateAndThrowAsync(createRoom);
        var game = await _gameService.GetAsync(createRoom.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        // Get last room order number
        var lastRoomInfo = await _repository.GetLastRoomInfo(createRoom.GameId);
        var orderNumber = (lastRoomInfo?.OrderNumber ?? 0) + 1;

        var entity = new CreateRoomEntity
        {
            RoomId = _guidFactory.Create(),
            GameId = createRoom.GameId,
            Title = createRoom.Title.Trim(),
            Type = createRoom.Type,
            AccessType = createRoom.AccessType,
            ViewPrivateText = createRoom.ViewPrivateText,
            ViewDiceResults = createRoom.ViewDiceResults,
            DiceEnabled = createRoom.DiceEnabled,
            OrderNumber = orderNumber
        };

        var room = await _repository.Create(entity);
        await _unreadCountersRepository.CreateAsync(room.Id, game.Id, UnreadEntryType.Message);
        await _producer.SendAsync(EventType.NewRoom, room.Id);

        return room;
    }

    #endregion

    #region Read

    public async Task<IEnumerable<Room>> GetAllAsync(Guid gameId)
    {
        await _gameService.GetAsync(gameId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var rooms = (await _repository.GetAllVisible(gameId, currentUserId)).ToArray();

        await _unreadCountersRepository.FillEntityCounters(rooms, currentUserId,
            r => r.Id, r => r.UnreadPostsCount);

        // A room the reader may not open is listed by its name and nothing
        // else: who has access to it, whose turn it is inside and how much of
        // it is unread are all things only its readers get to know.
        foreach (var room in rooms.Where(r => !r.CanView))
        {
            room.Accesses = [];
            room.Pendencies = [];
            room.TotalPostsCount = 0;
            room.UnreadPostsCount = 0;
        }

        return rooms;
    }

    public async Task<Room> GetAsync(Guid roomId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var room = await _repository.GetAvailable(roomId, currentUserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RoomNotFound);
        }

        await _unreadCountersRepository.FillEntityCounters(new[] { room }, currentUserId,
            r => r.Id, r => r.UnreadPostsCount);

        return room;
    }

    /// <inheritdoc />
    public async Task<RoomToUpdate> GetWithGameAsync(Guid roomId)
    {
        var room = await _repository.GetForUpdate(roomId, _identityProvider.Current.User.UserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RoomNotFound);
        }

        return room;
    }

    /// <inheritdoc />
    public Task<RoomToUpdate> GetChatRoomForReadingAsync(Guid roomId) =>
        GetChatRoomAsync(roomId, RoomIntention.ViewMessages);

    /// <inheritdoc />
    public Task<RoomToUpdate> GetChatRoomForWritingAsync(Guid roomId) =>
        GetChatRoomAsync(roomId, RoomIntention.SendMessage);

    /// <summary>
    /// A room of another type, or a chat room with no chat behind it, is absent
    /// rather than forbidden: the caller asked for a chat and there is none at that
    /// address, and a refusal would confirm the address exists.
    /// </summary>
    /// <remarks>
    /// Read through GetForUpdate because it is the projection that carries the game,
    /// and every room rule reads the roles of the game. Asked with the plain one the
    /// check finds no resolver and refuses everybody.
    /// </remarks>
    private async Task<RoomToUpdate> GetChatRoomAsync(Guid roomId, RoomIntention intention)
    {
        var room = await _repository.GetForUpdate(roomId, _identityProvider.Current.User.UserId);
        if (room == null || room.Type != RoomType.Chat || !room.ChatId.HasValue)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
        }

        _intentionManager.ThrowIfForbidden(intention, room);

        return room;
    }

    #endregion

    #region Update

    public async Task<Room> UpdateAsync(UpdateRoom updateRoom)
    {
        await _updateValidator.ValidateAndThrowAsync(updateRoom);
        var currentUserId = _identityProvider.Current.User.UserId;
        var room = await _repository.GetForUpdate(updateRoom.RoomId, currentUserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RoomNotFound);
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        var entity = new UpdateRoomEntity
        {
            RoomId = updateRoom.RoomId,
            Title = updateRoom.Title,
            Type = updateRoom.Type,
            AccessType = updateRoom.AccessType,
            ViewPrivateText = updateRoom.ViewPrivateText,
            ViewDiceResults = updateRoom.ViewDiceResults,
            DiceEnabled = updateRoom.DiceEnabled,
            HiddenWithoutAccess = updateRoom.HiddenWithoutAccess,
            IsArchived = updateRoom.IsArchived,
            IsRemoved = updateRoom.IsRemoved,
            ShouldReorder = updateRoom.PreviousRoomId != null,
            NewPreviousRoomId = updateRoom.PreviousRoomId?.Value,
            ChatId = updateRoom.ChatId,
            ShouldSetChatId = updateRoom.ChatId.HasValue
        };

        var result = await _repository.Update(entity);
        await _producer.SendAsync(EventType.ChangedRoom, result.Id);

        return result;
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid roomId)
    {
        var room = await _repository.GetForUpdate(roomId, _identityProvider.Current.User.UserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RoomNotFound);
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        await _repository.Delete(roomId);
        await _unreadCountersRepository.DeleteAsync(roomId, UnreadEntryType.Message);
        await _producer.SendAsync(EventType.DeletedRoom, roomId);
    }

    #endregion
}
