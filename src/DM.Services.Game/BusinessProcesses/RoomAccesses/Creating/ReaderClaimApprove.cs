using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Exceptions;
using DM.Services.Game.Dto.Internal;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

/// <inheritdoc />
internal class ReaderClaimApprove : IReaderClaimApprove
{
    private readonly IRoomAccessCreatingRepository _creatingRepository;

    /// <inheritdoc />
    public ReaderClaimApprove(
        IRoomAccessCreatingRepository creatingRepository)
    {
        _creatingRepository = creatingRepository;
    }

    /// <inheritdoc />
    public async Task<Guid> GetParticipantId(string readerLogin, RoomToUpdate room)
    {
        var readerId = await _creatingRepository.FindReaderId(room.Game.Id, readerLogin);
        if (!readerId.HasValue)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(RoomAccess.User)] = ValidationError.Invalid
            });
        }

        return readerId.Value;
    }
}