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
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Game.Features.Rooms;

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

        var roomId = _guidFactory.Create();
        var entity = new CreateRoomEntity
        {
            RoomId = roomId,
            GameId = createRoom.GameId,
            Title = createRoom.Title,
            Type = createRoom.Type,
            AccessType = createRoom.AccessType,
            ViewPrivateText = createRoom.ViewPrivateText,
            ViewDiceResults = createRoom.ViewDiceResults,
            DiceEnabled = createRoom.DiceEnabled
        };

        // Markers first, row second, commit on the line after it returns.
        await using var counters = await _unreadCountersRepository.ReserveAsync(
            UnreadMarker.UnderParent(roomId, game.Id, UnreadEntryType.Message));

        var room = await _repository.Create(entity);
        counters.Commit();

        await _producer.SendAsync(EventType.NewRoom, room.Id);

        return room;
    }

    #endregion

    #region Read

    // As in GameService: the game and its rooms are one page for the premoderation judge.
    private bool MayJudgePremoderation =>
        _intentionManager.IsAllowed(GameIntention.SetStatusModeration);

    public async Task<IEnumerable<Room>> GetAllAsync(Guid gameId)
    {
        await _gameService.GetAsync(gameId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var rooms = (await _repository.GetAllVisible(
            gameId, currentUserId, MayJudgePremoderation)).ToArray();

        await _unreadCountersRepository.FillEntityCounters(rooms, currentUserId,
            r => r.Id, r => r.UnreadPostsCount);

        // A closed room shows its name and its writers' names, already public on the character list;
        // the rest is for its readers.
        foreach (var room in rooms.Where(r => !r.CanView))
        {
            room.CharacterNames = WriterNames(room);
            room.Accesses = [];
            room.Pendencies = [];
            room.TotalPostsCount = 0;
            room.UnreadPostsCount = 0;
        }

        return rooms;
    }

    // The grant is the whole test, whatever became of the character since, as on the old site.
    private static string[] WriterNames(Room room) => room.Accesses
        .Where(a => a.TargetType == RoomAccessTargetType.Character
                    && a.Policy == RoomAccessPolicy.Full)
        .Select(a => a.Character)
        .OfType<Character>()
        .OrderBy(c => c.CreatedUtc)
        .ThenBy(c => c.Name, StringComparer.Ordinal)
        .Select(c => c.Name)
        .ToArray();

    public async Task<Room> GetAsync(Guid roomId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var room = await _repository.GetAvailable(roomId, currentUserId, MayJudgePremoderation);
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
    /// A room of another type, or without a chat, is absent rather than forbidden.
    /// </summary>
    /// <remarks>
    /// GetForUpdate carries the game, whose roles every room rule reads.
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
        var room = await GetWithGameAsync(updateRoom.RoomId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        if (updateRoom.PreviousRoomId?.Value is { } previousRoomId)
        {
            await ThrowIfNotAPlaceInGame(room, previousRoomId);
        }

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
            ShouldReorder = updateRoom.PreviousRoomId != null,
            NewPreviousRoomId = updateRoom.PreviousRoomId?.Value,
            ChatId = updateRoom.ChatId,
            ShouldSetChatId = updateRoom.ChatId.HasValue
        };

        var result = await _repository.Update(entity);
        await _producer.SendAsync(EventType.ChangedRoom, result.Id);

        return result;
    }

    // Any id but a room of the same game is refused: taken silently, the move would change nothing.
    private async Task ThrowIfNotAPlaceInGame(Room room, Guid previousRoomId)
    {
        var previous = previousRoomId == room.Id
            ? null
            : await _repository.GetForUpdate(previousRoomId, _identityProvider.Current.User.UserId);
        if (previous == null || previous.GameId != room.GameId)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["previousRoomId"] = ValidationError.Invalid
            });
        }
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid roomId)
    {
        var room = await GetWithGameAsync(roomId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        await _repository.Delete(roomId, _identityProvider.Current.User.UserId);
        await _unreadCountersRepository.DeleteAsync(roomId, UnreadEntryType.Message);
        await _producer.SendAsync(EventType.DeletedRoom, roomId);
    }

    #endregion
}
