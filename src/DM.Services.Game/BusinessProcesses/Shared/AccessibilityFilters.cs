using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Services.Core.Dto.Enums;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;

namespace DM.Services.Game.BusinessProcesses.Shared;

/// <summary>
/// Filters for blacklist and
/// </summary>
public static class AccessibilityFilters
{
    /// <summary>
    /// Game is accessible for user
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public static Expression<Func<DbGame, bool>> GameAvailable(Guid userId) => game =>
        !game.IsRemoved &&
        !(
            game.BlackList != null &&
            game.BlackList.Any(b => b.UserId == userId)
        ) &&
        (
            game.MasterId == userId ||
            game.AssistantId == userId ||
            game.MentorId == userId ||
            (game.Status != ModuleStatus.Draft &&
             game.PremoderationStatus == PremoderationStatus.Approved)
        );

    /// <summary>
    /// Room is accessible for user
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public static Expression<Func<DbRoom, bool>> RoomAvailable(Guid userId) => room =>
        !room.IsRemoved &&
        !room.Game.IsRemoved &&
        !(
            room.Game.BlackList != null &&
            room.Game.BlackList.Any(b => b.UserId == userId)
        ) &&
        (
            room.Game.MasterId == userId ||
            room.Game.AssistantId == userId ||
            room.Game.MentorId == userId ||
            (room.Game.Status != ModuleStatus.Draft &&
             room.Game.PremoderationStatus == PremoderationStatus.Approved)
        ) &&
        (
            room.AccessType == RoomAccessType.Open ||
            room.AccessType == RoomAccessType.Secret &&
            (
                room.Game.MasterId == userId ||
                room.Game.AssistantId == userId
            ) ||
            room.RoomAccesses.Any(l => (l.Character != null && l.Character.UserId == userId) || (l.Reader != null && l.Reader.UserId == userId))
        );
}