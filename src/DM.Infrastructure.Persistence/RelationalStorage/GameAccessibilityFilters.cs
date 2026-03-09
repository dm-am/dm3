using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Filters for game blacklist and visibility
/// </summary>
public static class GameAccessibilityFilters
{
    /// <summary>
    /// Game is accessible for user
    /// </summary>
    public static Expression<Func<DbGame, bool>> GameAvailable(Guid userId) => game =>
        !game.IsRemoved &&
        !(
            game.BlackList != null &&
            game.BlackList.Any(b => b.BlockedUserId == userId)
        ) &&
        (
            game.AuthorId == userId ||
            game.Assistants.Any(a => a.UserId == userId) ||
            game.MentorId == userId ||
            // User has pending invitation (player, reader, or assistant)
            game.Tokens.Any(t =>
                t.UserId == userId &&
                !t.IsRemoved &&
                (t.Type == TokenType.GamePlayerInvitation ||
                 t.Type == TokenType.GameReaderInvitation ||
                 t.Type == TokenType.GameAssistantInvitation)) ||
            (game.Status != ModuleStatus.Draft &&
             game.PremoderationStatus == PremoderationStatus.Approved)
        );

    /// <summary>
    /// Room is accessible for user
    /// </summary>
    public static Expression<Func<DbRoom, bool>> RoomAvailable(Guid userId) => room =>
        !room.IsRemoved &&
        !room.Game.IsRemoved &&
        !(
            room.Game.BlackList != null &&
            room.Game.BlackList.Any(b => b.BlockedUserId == userId)
        ) &&
        (
            room.Game.AuthorId == userId ||
            room.Game.Assistants.Any(a => a.UserId == userId) ||
            room.Game.MentorId == userId ||
            (room.Game.Status != ModuleStatus.Draft &&
             room.Game.PremoderationStatus == PremoderationStatus.Approved)
        ) &&
        (
            room.AccessType == RoomAccessType.Open ||
            room.AccessType == RoomAccessType.Private &&
            (
                room.Game.AuthorId == userId ||
                room.Game.Assistants.Any(a => a.UserId == userId)
            ) ||
            room.RoomAccesses.Any(l => (l.Character != null && l.Character.AuthorId == userId) || l.ReaderUserId == userId)
        );
}
