using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <summary>
/// Repository for room access operations
/// </summary>
public interface IRoomAccessRepository
{
    #region Read

    /// <summary>
    /// Get all accesses for a game
    /// </summary>
    Task<IEnumerable<RoomAccess>> GetGameAccesses(Guid gameId, Guid userId);

    /// <summary>
    /// Get all accesses for a room
    /// </summary>
    Task<IEnumerable<RoomAccess>> GetRoomAccesses(Guid roomId, Guid userId);

    /// <summary>
    /// Get single access
    /// </summary>
    Task<RoomAccess?> GetAccess(Guid accessId, Guid userId);

    /// <summary>
    /// Find reader user ID by game and username
    /// </summary>
    Task<Guid?> FindReaderUserId(Guid gameId, string readerUsername);

    /// <summary>
    /// Find character's game ID
    /// </summary>
    Task<Guid?> FindCharacterGameId(Guid characterId);

    #endregion

    #region Write

    /// <summary>
    /// Create room access
    /// </summary>
    Task<RoomAccess> Create(CreateRoomAccessEntity entity);

    /// <summary>
    /// Update room access
    /// </summary>
    Task<RoomAccess> Update(UpdateRoomAccessEntity entity);

    /// <summary>
    /// Delete room access
    /// </summary>
    Task Delete(Guid accessId);

    #endregion
}
