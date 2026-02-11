using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Reading;

/// <summary>
/// Storage for room accesses reading
/// </summary>
internal interface IRoomAccessReadingRepository
{
    /// <summary>
    /// Get all game accesses
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<IEnumerable<RoomAccess>> GetGameAccesses(Guid gameId, Guid userId);

    /// <summary>
    /// Get all room accesses
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<IEnumerable<RoomAccess>> GetRoomAccesses(Guid roomId, Guid userId);

    /// <summary>
    /// Get existing room access
    /// </summary>
    /// <param name="accessId">Access identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<RoomAccess?> GetAccess(Guid accessId, Guid userId);
}