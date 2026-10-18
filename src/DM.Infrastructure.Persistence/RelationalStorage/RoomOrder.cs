using System.Linq;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// The order the rooms of a game stand in, for every read that lists them or
/// walks them one after another.
/// </summary>
/// <remarks>
/// One text for all of them: the menu and the jump to the first unread post
/// would otherwise disagree about which room comes first.
/// </remarks>
public static class RoomOrder
{
    /// <summary>
    /// By OrderNumber, the place the master gave the room. A room is given a
    /// place of its own when it is created and when the rooms are moved, both
    /// under the lock of the game, so two rooms share one only when it was
    /// written by hand or a removed room came back; the room created first
    /// then goes first.
    /// </summary>
    public static IOrderedQueryable<DbRoom> InGameOrder(this IQueryable<DbRoom> rooms) =>
        rooms.OrderBy(r => r.OrderNumber).ThenBy(r => r.RoomNumber);
}
