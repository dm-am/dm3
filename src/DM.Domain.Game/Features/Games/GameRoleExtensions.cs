using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Game.Features.Games;

public static class GameRoleExtensions
{
    /// <summary>
    /// Master or assistant
    /// </summary>
    public static bool HasEditAccess(this IEnumerable<GameRole> roles) =>
        roles.Contains(GameRole.Master) || roles.Contains(GameRole.Assistant);

    /// <remarks>
    /// <paramref name="userId"/> is the user the game was read for: <see cref="GameRole.Reader" /> comes from
    /// the viewer-scoped <see cref="Game.IsViewerSubscriber" />, so another id would get the viewer's readership.
    /// </remarks>
    public static IReadOnlyCollection<GameRole> GetRoles(this Game game, Guid userId)
    {
        var roles = new List<GameRole>();

        if (game.Master?.UserId == userId)
        {
            roles.Add(GameRole.Master);
        }

        if (game.Assistants.Any(a => a.UserId == userId))
        {
            roles.Add(GameRole.Assistant);
        }

        if (game.Mentor?.UserId == userId)
        {
            roles.Add(GameRole.Mentor);
        }

        if (game.Players.Any(p => p.UserId == userId))
        {
            roles.Add(GameRole.Player);
        }

        if (game.IsViewerSubscriber)
        {
            roles.Add(GameRole.Reader);
        }

        return roles;
    }

    /// <summary>
    /// The predicate behind the ordinary ban's exemption.
    /// </summary>
    /// <remarks>
    /// Roles are listed by name, so a new <see cref="GameRole" /> does not widen the exemption by itself.
    /// A Reader is out: subscribing is self-service and would undo the ban by a button press.
    /// </remarks>
    public static bool IsOwnGame(this IEnumerable<GameRole> roles) =>
        roles.Any(r => r is GameRole.Master or GameRole.Assistant or GameRole.Mentor or GameRole.Player);

    /// <summary>
    /// Player or reader invitation
    /// </summary>
    public static bool HasPendingInvitation(this Game game, Guid userId) =>
        game.PendingInvitedUserIds.Contains(userId);

    public static bool HasPendingPlayerInvitation(this Game game, Guid userId) =>
        game.PendingPlayerInvitedUserIds.Contains(userId);

    /// <summary>
    /// The rule of the game's blacklist, written once.
    /// </summary>
    /// <remarks>
    /// It closes the discussion and the application and nothing else: who writes in the rooms is decided
    /// by the rooms and the roster, and a review or a rating is feedback a moderator answers for.
    /// </remarks>
    public static bool IsBlacklisted(this Game game, Guid userId) =>
        game.BlacklistedUsers.Any(b => b.UserId == userId);

    /// <summary>
    /// Asked before the intention, so the refusal says why instead of "not enough rights".
    /// </summary>
    public static void ThrowIfBlacklisted(this Game game, Guid userId)
    {
        if (game.IsBlacklisted(userId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromGame);
        }
    }

    /// <remarks>
    /// No default arm, so CS8509 names a member added later.
    /// </remarks>
    public static string ToApiString(this GameRole role) => role switch
    {
        GameRole.Master => "master",
        GameRole.Assistant => "assistant",
        GameRole.Mentor => "mentor",
        GameRole.Player => "player",
        GameRole.Reader => "reader",
        GameRole.None => "none"
    };
}
