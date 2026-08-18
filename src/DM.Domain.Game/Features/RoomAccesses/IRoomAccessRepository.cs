using System;
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

    /// <summary>
    /// Check whether an access for the same room and target (character or reader) already exists
    /// </summary>
    Task<bool> AccessExists(Guid roomId, Guid? characterId, Guid? readerUserId);

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
