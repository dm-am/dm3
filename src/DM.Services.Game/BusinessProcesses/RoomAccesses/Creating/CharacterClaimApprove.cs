using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Exceptions;
using DM.Services.Game.Dto.Internal;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

/// <inheritdoc />
internal class CharacterClaimApprove : ICharacterClaimApprove
{
    private readonly IRoomAccessCreatingRepository _repository;

    /// <inheritdoc />
    public CharacterClaimApprove(
        IRoomAccessCreatingRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Guid> GetParticipantId(Guid characterId, RoomToUpdate room)
    {
        var gameId = await _repository.FindCharacterGameId(characterId);
        if (!gameId.HasValue || room.Game.Id != gameId)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(RoomAccess.Character)] = ValidationError.Invalid
            });
        }

        return characterId;
    }
}