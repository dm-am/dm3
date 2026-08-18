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
    /// Get every room of a game the user may SEE, each flagged with whether
    /// they may open it (Room.CanView). A private room they have no access to
    /// comes back by name, so the menu can show it closed.
    /// </summary>
    /// <param name="gameId">Game whose rooms are listed.</param>
    /// <param name="userId">Reader.</param>
    /// <param name="mayJudgePremoderation">
    /// Whether the reader holds the rank that passes premoderation verdicts,
    /// asked of the intention by the caller and handed down, because the storage
    /// scope cannot ask it. Adds the rooms of the games awaiting a verdict and
    /// nothing else: a judge who is handed the game and an empty room list has
    /// nothing to judge.
    /// </param>
    Task<IEnumerable<Room>> GetAllVisible(Guid gameId, Guid userId, bool mayJudgePremoderation);

    /// <summary>
    /// Get single available room
    /// </summary>
    /// <inheritdoc cref="GetAllVisible" path="/param[@name='mayJudgePremoderation']" />
    Task<Room?> GetAvailable(Guid roomId, Guid userId, bool mayJudgePremoderation);

    /// <summary>
    /// Get room for update
    /// </summary>
    Task<RoomToUpdate?> GetForUpdate(Guid roomId, Guid userId);

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
    /// <param name="roomId">Room identifier.</param>
    /// <param name="deletedByUserId">Who pressed delete.</param>
    Task Delete(Guid roomId, Guid deletedByUserId);

    #endregion
}
