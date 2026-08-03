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
            game.MasterId == userId ||
            game.Assistants.Any(a => a.UserId == userId) ||
            game.MentorId == userId ||
            // User has pending invitation (player, reader, or assistant)
            game.Tokens.Any(t =>
                t.UserId == userId &&
                !t.IsRemoved &&
                (t.Type == TokenType.GamePlayerInvitation ||
                 t.Type == TokenType.GameReaderInvitation ||
                 t.Type == TokenType.GameAssistantInvitation)) ||
            // Public games (non-draft with approved premoderation, or public draft with approved premoderation)
            (game.PremoderationStatus == PremoderationStatus.Approved &&
             (game.Status != ModuleStatus.Draft ||
              game.DraftVisibility == DraftVisibility.Public))
        );

    /// <summary>
    /// Room is accessible for user
    /// </summary>
    /// <param name="userId">Reader</param>
    /// <param name="listingOnly">
    /// Answer only whether the room may be NAMED in the game's room list,
    /// skipping the room's own access type. The rooms menu shows private rooms
    /// the reader may not enter (closed lock, plain text instead of a link), so
    /// "listed" and "may be opened" are two questions over one rule; every
    /// point read keeps the default and stays closed.
    /// </param>
    public static Expression<Func<DbRoom, bool>> RoomAvailable(
        Guid userId, bool listingOnly = false) => room =>
        !room.IsRemoved &&
        !room.Game.IsRemoved &&
        !(
            room.Game.BlackList != null &&
            room.Game.BlackList.Any(b => b.BlockedUserId == userId)
        ) &&
        (
            room.Game.MasterId == userId ||
            room.Game.Assistants.Any(a => a.UserId == userId) ||
            room.Game.MentorId == userId ||
            // Public games (non-draft with approved premoderation, or public draft with approved premoderation)
            (room.Game.PremoderationStatus == PremoderationStatus.Approved &&
             (room.Game.Status != ModuleStatus.Draft ||
              room.Game.DraftVisibility == DraftVisibility.Public))
        ) &&
        (
            listingOnly ||
            room.AccessType == RoomAccessType.Open ||
            room.AccessType == RoomAccessType.Private &&
            (
                room.Game.MasterId == userId ||
                room.Game.Assistants.Any(a => a.UserId == userId)
            ) ||
            room.RoomAccesses.Any(l => (l.Character != null && l.Character.AuthorId == userId) || l.ReaderUserId == userId)
        );
}
