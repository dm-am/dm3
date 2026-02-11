using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Reading;

/// <summary>
/// Service for reading room accesses
/// </summary>
public interface IRoomAccessReadingService
{
    /// <summary>
    /// Get all game accesses
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns></returns>
    Task<IEnumerable<RoomAccess>> GetGameAccesses(Guid gameId);

    /// <summary>
    /// Get all room accesses
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <returns></returns>
    Task<IEnumerable<RoomAccess>> GetRoomAccesses(Guid roomId);

    /// <summary>
    /// Get existing access
    /// </summary>
    /// <param name="accessId">Access identifier</param>
    /// <returns></returns>
    Task<RoomAccess> GetAccess(Guid accessId);
}