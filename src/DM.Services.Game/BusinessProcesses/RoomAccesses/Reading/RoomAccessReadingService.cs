using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Exceptions;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Reading;

/// <inheritdoc />
internal class RoomAccessReadingService : IRoomAccessReadingService
{
    private readonly IRoomAccessReadingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomAccessReadingService(
        IRoomAccessReadingRepository repository,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public Task<IEnumerable<RoomAccess>> GetGameAccesses(Guid gameId) =>
        _repository.GetGameAccesses(gameId, _identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public Task<IEnumerable<RoomAccess>> GetRoomAccesses(Guid roomId) =>
        _repository.GetRoomAccesses(roomId, _identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public async Task<RoomAccess> GetAccess(Guid accessId)
    {
        var access = await _repository.GetAccess(accessId, _identityProvider.Current.User.UserId);
        if (access == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Access not found");
        }

        return access;
    }
}