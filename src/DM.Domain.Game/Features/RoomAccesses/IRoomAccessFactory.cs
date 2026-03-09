using System;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.RoomAccesses;
/// <summary>
/// Factory for room access entity DTO
/// </summary>
internal interface IRoomAccessFactory
{
    /// <summary>
    /// Create entity DTO for character access
    /// </summary>
    /// <param name="roomAccess">Input DTO</param>
    /// <param name="characterId">Character identifier</param>
    CreateRoomAccessEntity CreateForCharacter(CreateRoomAccess roomAccess, Guid characterId);
    /// <summary>
    /// Create entity DTO for reader user access
    /// </summary>
    /// <param name="roomAccess">Input DTO</param>
    /// <param name="readerUserId">Reader user identifier</param>
    CreateRoomAccessEntity CreateForReader(CreateRoomAccess roomAccess, Guid readerUserId);
}
