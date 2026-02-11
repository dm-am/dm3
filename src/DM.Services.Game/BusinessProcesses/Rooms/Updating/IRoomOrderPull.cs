using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Internal;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;

namespace DM.Services.Game.BusinessProcesses.Rooms.Updating;

/// <summary>
/// Factory for room neighbours changes on room order move
/// </summary>
internal interface IRoomOrderPull
{
    /// <summary>
    /// Get room neighbours changes on pull
    /// </summary>
    /// <param name="room">Room</param>
    /// <returns></returns>
    (IUpdateBuilder<DbRoom>? updateOldPrevious, IUpdateBuilder<DbRoom>? updateOldNext) GetPullChanges(
        RoomToUpdate room);
}