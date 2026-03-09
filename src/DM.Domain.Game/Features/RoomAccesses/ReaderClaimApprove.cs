using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.RoomAccesses;
/// <inheritdoc />
internal class ReaderClaimApprove : IReaderClaimApprove
{
    private readonly IRoomAccessRepository _repository;
    /// <inheritdoc />
    public ReaderClaimApprove(
        IRoomAccessRepository repository)
    {
        _repository = repository;
    }
    public async Task<Guid> GetReaderUserId(string readerUsername, RoomToUpdate room)
    {
        var readerUserId = await _repository.FindReaderUserId(room.Game.Id, readerUsername);
        if (!readerUserId.HasValue)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(RoomAccess.User)] = ValidationError.Invalid
            });
        }
        return readerUserId.Value;
    }
}
