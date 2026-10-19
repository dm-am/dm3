using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Queries;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Filters for game visibility
/// </summary>
/// <remarks>
/// The game's blacklist is not one of them: it closes writing and leaves reading
/// alone, as GameIntentionResolver.Read does.
/// </remarks>
public static class GameAccessibilityFilters
{
    /// <summary>
    /// ModuleVisibility.IsPubliclyVisible as a tree EF can translate;
    /// GameAccessibilityFiltersShould keeps the two equal.
    /// </summary>
    private static readonly Expression<Func<DbGame, bool>> PubliclyVisible = game =>
        game.PremoderationStatus == PremoderationStatus.Approved &&
        (game.Status != ModuleStatus.Draft || game.DraftVisibility == DraftVisibility.Public);

    /// <summary>
    /// Awaiting a premoderation verdict, by the premoderation status alone, so a private draft stays shut.
    /// </summary>
    private static readonly Expression<Func<DbGame, bool>> AwaitingVerdict = game =>
        game.PremoderationStatus == PremoderationStatus.AwaitingApproval ||
        game.PremoderationStatus == PremoderationStatus.AwaitingEdits;

    /// <summary>
    /// Game is accessible for user
    /// </summary>
    /// <param name="userId">Reader</param>
    /// <param name="mayJudgePremoderation">
    /// Whether the reader holds the rank that passes premoderation verdicts
    /// (<see cref="DM.Domain.Core.Authorization.PremoderationAccess.MayJudgePremoderation" />).
    /// Opens games awaiting a verdict and nothing else; false for every list read.
    /// </param>
    public static Expression<Func<DbGame, bool>> GameAvailable(
        Guid userId, bool mayJudgePremoderation = false) => ExpressionSplicer.Expand<Func<DbGame, bool>>(game =>
        !game.IsRemoved &&
        (
            game.MasterId == userId ||
            game.Assistants.Any(a => a.UserId == userId) ||
            game.MentorId == userId ||
            (mayJudgePremoderation && AwaitingVerdict.Splice(game)) ||
            // A pending invitation (player, reader, or assistant)
            game.Tokens.Any(t =>
                t.UserId == userId &&
                !t.IsRemoved &&
                (t.Type == TokenType.GamePlayerInvitation ||
                 t.Type == TokenType.GameReaderInvitation ||
                 t.Type == TokenType.GameAssistantInvitation)) ||
            PubliclyVisible.Splice(game)
        ));

    /// <summary>
    /// Room is accessible for user
    /// </summary>
    /// <param name="userId">Reader</param>
    /// <param name="listingOnly">
    /// Answer only whether the room may be named in the game's room list, skipping
    /// the room's own access type, unless its master set HiddenWithoutAccess.
    /// </param>
    /// <param name="mayJudgePremoderation">
    /// The same parameter <see cref="GameAvailable" /> takes, about the room's game.
    /// </param>
    public static Expression<Func<DbRoom, bool>> RoomAvailable(
        Guid userId, bool listingOnly = false, bool mayJudgePremoderation = false) =>
        ExpressionSplicer.Expand<Func<DbRoom, bool>>(room =>
        !room.IsRemoved &&
        !room.Game.IsRemoved &&
        (
            room.Game.MasterId == userId ||
            room.Game.Assistants.Any(a => a.UserId == userId) ||
            room.Game.MentorId == userId ||
            // The game half only: the judge reads the rooms as any reader would.
            (mayJudgePremoderation && AwaitingVerdict.Splice(room.Game)) ||
            PubliclyVisible.Splice(room.Game)
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
        ));
}
