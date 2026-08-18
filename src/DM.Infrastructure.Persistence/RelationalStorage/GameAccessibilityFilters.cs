using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Filters for game visibility
/// </summary>
/// <remarks>
/// The game's blacklist is deliberately not one of them. It closes writing and
/// leaves reading alone: the game stays public to everybody else, so hiding it
/// from one person promises a privacy the game does not have. Both filters used
/// to drop a blacklisted user's row, which answered differently from
/// GameIntentionResolver.Read, which never consulted the list at all: the game
/// vanished from the list, from search and from its own address while the page
/// rule would have opened it. Every refusal the blacklist does produce sits on a
/// write path and carries RefusalMessage.BlacklistedFromGame.
/// </remarks>
public static class GameAccessibilityFilters
{
    /// <summary>
    /// Game is accessible for user
    /// </summary>
    /// <param name="userId">Reader</param>
    /// <param name="mayJudgePremoderation">
    /// Whether the reader holds the site-wide rank that passes premoderation
    /// verdicts (<see cref="DM.Domain.Core.Authorization.PremoderationAccess.MayJudgePremoderation" />,
    /// asked of the intention and handed down, because a filter cannot ask it
    /// itself). It opens games awaiting a verdict and nothing else — see the arm
    /// below. Defaults to false, which is the answer for every list read: the
    /// moderation worklist has its own scope and a mentor's ordinary game list
    /// must not silently fill with other people's unapproved games.
    /// </param>
    public static Expression<Func<DbGame, bool>> GameAvailable(
        Guid userId, bool mayJudgePremoderation = false) => game =>
        !game.IsRemoved &&
        (
            game.MasterId == userId ||
            game.Assistants.Any(a => a.UserId == userId) ||
            game.MentorId == userId ||
            // Whoever may pass the verdict may open the game the verdict is
            // pending on. Not "may open hidden games": the arm is keyed on the
            // premoderation status alone, so a private draft stays shut and a
            // removed game is already gone above. Restated as a tree for the same
            // reason as the visibility rule below, and pinned by the same test.
            (mayJudgePremoderation &&
             (game.PremoderationStatus == PremoderationStatus.AwaitingApproval ||
              game.PremoderationStatus == PremoderationStatus.AwaitingEdits)) ||
            // User has pending invitation (player, reader, or assistant)
            game.Tokens.Any(t =>
                t.UserId == userId &&
                !t.IsRemoved &&
                (t.Type == TokenType.GamePlayerInvitation ||
                 t.Type == TokenType.GameReaderInvitation ||
                 t.Type == TokenType.GameAssistantInvitation)) ||
            // The same rule as ModuleVisibility.IsPubliclyVisible, restated as an
            // expression tree because EF Core translates trees and not method
            // calls. GameAccessibilityFiltersShould compares the two over every
            // combination of the three fields, so the copies cannot drift.
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
    /// point read keeps the default and stays closed. A room whose master set
    /// HiddenWithoutAccess is the one exception: the switch takes it off the
    /// list of everybody who may not open it, so it has to cut here, in the
    /// query, and not on the client over a payload that still carries the room.
    /// </param>
    /// <param name="mayJudgePremoderation">
    /// The same parameter <see cref="GameAvailable" /> takes, answering the same
    /// question about the same game: whether the reader holds the rank that
    /// passes premoderation verdicts. Handed down rather than asked here,
    /// because a filter cannot ask it. Defaults to false, so every read that
    /// does not serve the verdict — the pulse, search, first-unread — keeps
    /// answering the public rule alone.
    /// </param>
    public static Expression<Func<DbRoom, bool>> RoomAvailable(
        Guid userId, bool listingOnly = false, bool mayJudgePremoderation = false) => room =>
        !room.IsRemoved &&
        !room.Game.IsRemoved &&
        (
            room.Game.MasterId == userId ||
            room.Game.Assistants.Any(a => a.UserId == userId) ||
            room.Game.MentorId == userId ||
            // The same arm GameAvailable carries, over the game the room belongs
            // to: whoever may pass the verdict on a game may open its rooms,
            // because a game whose room list is empty cannot be judged. Keyed on
            // the premoderation status alone — the set PremoderationAccess.IsPending
            // names — so a private draft stays shut and a removed game is already
            // gone above. Restated as a tree for the same reason as the rule below.
            //
            // The game half only. The room's own access type is decided by the
            // block after this one and the rank is not one of its arms: the judge
            // reads the game the way any reader of it would, entering the open
            // rooms and seeing the closed ones named.
            (mayJudgePremoderation &&
             (room.Game.PremoderationStatus == PremoderationStatus.AwaitingApproval ||
              room.Game.PremoderationStatus == PremoderationStatus.AwaitingEdits)) ||
            // Visibility of the game the room belongs to: the same rule as
            // ModuleVisibility.IsPubliclyVisible, restated as an expression tree
            // for the same reason as in GameAvailable and pinned by the same test.
            (room.Game.PremoderationStatus == PremoderationStatus.Approved &&
             (room.Game.Status != ModuleStatus.Draft ||
              room.Game.DraftVisibility == DraftVisibility.Public))
        ) &&
        (
            listingOnly && !room.HiddenWithoutAccess ||
            room.AccessType == RoomAccessType.Open ||
            room.AccessType == RoomAccessType.Private &&
            (
                room.Game.MasterId == userId ||
                room.Game.Assistants.Any(a => a.UserId == userId)
            ) ||
            room.RoomAccesses.Any(l => (l.Character != null && l.Character.AuthorId == userId) || l.ReaderUserId == userId)
        );
}
