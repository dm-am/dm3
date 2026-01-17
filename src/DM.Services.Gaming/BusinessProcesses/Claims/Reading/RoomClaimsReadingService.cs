using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Exceptions;
using DM.Services.Gaming.Dto.Output;

namespace DM.Services.Gaming.BusinessProcesses.Claims.Reading;

/// <inheritdoc />
internal class RoomClaimsReadingService : IRoomClaimsReadingService
{
    private readonly IRoomClaimsReadingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomClaimsReadingService(
        IRoomClaimsReadingRepository repository,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public Task<IEnumerable<RoomClaim>> GetGameClaims(Guid gameId) =>
        _repository.GetGameClaims(gameId, _identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public Task<IEnumerable<RoomClaim>> GetRoomClaims(Guid roomId) =>
        _repository.GetRoomClaims(roomId, _identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public async Task<RoomClaim> GetClaim(Guid claimId)
    {
        var claim = await _repository.GetClaim(claimId, _identityProvider.Current.User.UserId);
        if (claim == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Claim not found");
        }

        return claim;
    }
}