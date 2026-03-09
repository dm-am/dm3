using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Rooms;

/// <summary>
/// Repository for room operations
/// </summary>
public interface IRoomRepository
{
    #region Read

    /// <summary>
    /// Get all available rooms for a game
    /// </summary>
    Task<IEnumerable<Room>> GetAllAvailable(Guid gameId, Guid userId);

    /// <summary>
    /// Get single available room
    /// </summary>
    Task<Room?> GetAvailable(Guid roomId, Guid userId);

    /// <summary>
    /// Get room for update
    /// </summary>
    Task<RoomToUpdate?> GetForUpdate(Guid roomId, Guid userId);

    /// <summary>
    /// Get room neighbours for reordering
    /// </summary>
    Task<RoomNeighbours> GetNeighbours(Guid roomId);

    /// <summary>
    /// Get first room info for a game
    /// </summary>
    Task<RoomOrderInfo?> GetFirstRoomInfo(Guid gameId);

    /// <summary>
    /// Get last room info for a game
    /// </summary>
    Task<RoomOrderInfo?> GetLastRoomInfo(Guid gameId);

    #endregion

    #region Write

    /// <summary>
    /// Create new room
    /// </summary>
    Task<Room> Create(CreateRoomEntity createRoom);

    /// <summary>
    /// Update room
    /// </summary>
    Task<Room> Update(UpdateRoomEntity updateRoom);

    /// <summary>
    /// Delete room
    /// </summary>
    Task Delete(Guid roomId);

    #endregion
}
