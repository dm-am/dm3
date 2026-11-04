using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Rooms;

public interface IRoomRepository
{
    /// <summary>
    /// Every room the user may see, flagged by Room.CanView: a closed one comes back by name for the menu.
    /// </summary>
    /// <remarks>
    /// <paramref name="mayJudgePremoderation"/> is asked of the intention by the caller, since the storage scope
    /// cannot ask it; it adds the rooms of the games awaiting a verdict and nothing else.
    /// </remarks>
    Task<IEnumerable<Room>> GetAllVisible(Guid gameId, Guid userId, bool mayJudgePremoderation);

    /// <inheritdoc cref="GetAllVisible" path="/remarks" />
    Task<Room?> GetAvailable(Guid roomId, Guid userId, bool mayJudgePremoderation);

    Task<RoomToUpdate?> GetForUpdate(Guid roomId, Guid userId);

    /// <summary>
    /// Numbered and placed last in its game by the repository
    /// </summary>
    Task<Room> Create(CreateRoomEntity createRoom);

    Task<Room> Update(UpdateRoomEntity updateRoom);

    Task Delete(Guid roomId, Guid deletedByUserId);
}
