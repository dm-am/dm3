using DM.Domain.Game.Features.Games;
using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.RoomAccesses;
/// <summary>
/// Validate and decide character for room access
/// </summary>
internal interface ICharacterClaimApprove
{
    /// <summary>
    /// Validate and return character identifier
    /// </summary>
    /// <param name="characterId">Character identifier</param>
    /// <param name="room">Room to update</param>
    /// <returns>Validated character ID</returns>
    Task<Guid> GetCharacterId(Guid characterId, RoomToUpdate room);
}
