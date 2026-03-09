using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace DM.Domain.Game.Features.RoomAccesses;

/// <summary>
/// Unified service for room access CRUD operations
/// </summary>
public interface IRoomAccessService
{
    #region Create

    /// <summary>
    /// Create new room access
    /// </summary>
    /// <param name="createRoomAccess">DTO model</param>
    Task<RoomAccess> CreateAsync(CreateRoomAccess createRoomAccess);

    #endregion

    #region Read

    /// <summary>
    /// Get all game accesses
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task<IEnumerable<RoomAccess>> GetGameAccessesAsync(Guid gameId);

    /// <summary>
    /// Get all room accesses
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    Task<IEnumerable<RoomAccess>> GetRoomAccessesAsync(Guid roomId);

    /// <summary>
    /// Get existing access
    /// </summary>
    /// <param name="accessId">Access identifier</param>
    Task<RoomAccess> GetAsync(Guid accessId);

    #endregion

    #region Update

    /// <summary>
    /// Update existing room access
    /// </summary>
    /// <param name="updateRoomAccess">DTO for update</param>
    Task<RoomAccess> UpdateAsync(UpdateRoomAccess updateRoomAccess);

    #endregion

    #region Delete

    /// <summary>
    /// Delete existing access
    /// </summary>
    /// <param name="accessId">Access identifier</param>
    Task DeleteAsync(Guid accessId);

    #endregion
}
