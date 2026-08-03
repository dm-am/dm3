using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace DM.Domain.Game.Features.Rooms;

/// <summary>
/// Unified service for room CRUD operations
/// </summary>
public interface IRoomService
{
    #region Create

    /// <summary>
    /// Create new room
    /// </summary>
    /// <param name="createRoom">DTO for room creating</param>
    Task<Room> CreateAsync(CreateRoom createRoom);

    #endregion

    #region Read

    /// <summary>
    /// Get all available game rooms
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task<IEnumerable<Room>> GetAllAsync(Guid gameId);

    /// <summary>
    /// Get single existing room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    Task<Room> GetAsync(Guid roomId);

    /// <summary>
    /// Get single existing room together with the game it belongs to
    /// </summary>
    /// <remarks>
    /// Every RoomIntention rule reads the roles of the game, and the plain
    /// projection does not carry it. Asked with that one the check finds no
    /// resolver and refuses everybody, which is a refusal nobody can act on.
    /// </remarks>
    /// <param name="roomId">Room identifier</param>
    Task<RoomToUpdate> GetWithGameAsync(Guid roomId);

    #endregion

    #region Update

    /// <summary>
    /// Update existing room
    /// </summary>
    /// <param name="updateRoom">DTO for room updating</param>
    Task<Room> UpdateAsync(UpdateRoom updateRoom);

    #endregion

    #region Delete

    /// <summary>
    /// Delete existing room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    Task DeleteAsync(Guid roomId);

    #endregion
}
