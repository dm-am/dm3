using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.RoomAccesses;

internal interface ICharacterClaimApprove
{
    /// <summary>
    /// The character, refused unless it belongs to the room's game
    /// </summary>
    Task<Guid> GetCharacterId(Guid characterId, RoomToUpdate room);
}
