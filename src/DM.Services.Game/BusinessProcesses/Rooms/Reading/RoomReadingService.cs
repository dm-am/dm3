using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Extensions;
using DM.Services.Core.Exceptions;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Rooms.Reading;

/// <inheritdoc />
internal class RoomReadingService : IRoomReadingService
{
    private readonly IGameReadingService _gameReadingService;
    private readonly IRoomReadingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomReadingService(
        IGameReadingService gameReadingService,
        IRoomReadingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider)
    {
        _gameReadingService = gameReadingService;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Room>> GetAll(Guid gameId)
    {
        await _gameReadingService.GetGame(gameId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var rooms = (await _repository.GetAllAvailable(gameId, currentUserId)).ToArray();
        await _unreadCountersRepository.FillEntityCounters(rooms, currentUserId,
            r => r.Id, r => r.UnreadPostsCount);
        return rooms;
    }

    /// <inheritdoc />
    public async Task<Room> Get(Guid roomId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var room = await _repository.GetAvailable(roomId, currentUserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Room not found");
        }

        await _unreadCountersRepository.FillEntityCounters(new[] {room}, currentUserId,
            r => r.Id, r => r.UnreadPostsCount);
        return room;
    }
}