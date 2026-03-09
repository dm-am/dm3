using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.RoomAccesses;
/// <inheritdoc />
internal class CharacterClaimApprove : ICharacterClaimApprove
{
    private readonly IRoomAccessRepository _repository;
    /// <inheritdoc />
    public CharacterClaimApprove(
        IRoomAccessRepository repository)
    {
        _repository = repository;
    }
    public async Task<Guid> GetCharacterId(Guid characterId, RoomToUpdate room)
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
