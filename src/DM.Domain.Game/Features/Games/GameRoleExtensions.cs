using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Extensions for game role resolving
/// </summary>
public static class GameRoleExtensions
{
    /// <summary>
    /// Checks if roles include edit access (master or assistant)
    /// </summary>
    public static bool HasEditAccess(this IEnumerable<GameRole> roles)
    {
        return roles.Contains(GameRole.Master) || roles.Contains(GameRole.Assistant);
    }

    /// <summary>
    /// Gets all roles a user has in a game
    /// </summary>
    /// <param name="game">Mapped game</param>
    /// <param name="userId">
    /// User identifier. Must be the user the game was read for.
    /// <see cref="GameRole.Reader" /> is taken from
    /// <see cref="Game.IsViewerSubscriber" />, which the repository fills for
    /// the viewer and which this method does not compare against
    /// <paramref name="userId" /> at all — so asking about anybody else answers
    /// the other four roles correctly and reports the VIEWER's readership as
    /// theirs. That is a false positive, not a missing role, which is why there
    /// is no call site passing a foreign id.
    /// </param>
    /// <returns>Collection of game roles</returns>
    public static IReadOnlyCollection<GameRole> GetRoles(this Game game, Guid userId)
    {
        var roles = new List<GameRole>();

        if (game.Master.UserId == userId)
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
    /// Gets the highest role a user has in a game
    /// </summary>
    public static GameRole GetHighestRole(this IEnumerable<GameRole> roles)
    {
        return roles.DefaultIfEmpty(GameRole.None).Max();
    }

    /// <summary>
    /// Checks if user has any role in the game (not None)
    /// </summary>
    public static bool HasAnyRole(this IEnumerable<GameRole> roles)
    {
        return roles.Any();
    }

    /// <summary>
    /// Whether the game is the user's own — the predicate behind the ordinary
    /// ban's exemption.
    /// </summary>
    /// <remarks>
    /// Master, assistant, a player with an accepted character, and the mentor
    /// curating the game. Everything except a plain Reader: a subscription is
    /// self-service, anyone may subscribe to any public game in one request, so
    /// it would undo the ban by a button press. An application in review is not
    /// here either — it puts nobody into Players, which only holds authors of
    /// accepted characters.
    ///
    /// Narrower than <see cref="HasAnyRole" />, which answers the different
    /// question of whether the user is connected to the game at all.
    /// </remarks>
    public static bool IsOwnGame(this IEnumerable<GameRole> roles)
    {
        return roles.Any(r => r is not GameRole.Reader);
    }

    /// <summary>
    /// Checks if user has a pending invitation (player or reader)
    /// </summary>
    public static bool HasPendingInvitation(this Game game, Guid userId)
    {
        return game.PendingInvitedUserIds.Contains(userId);
    }

    /// <summary>
    /// Checks if user has a pending PLAYER invitation
    /// </summary>
    public static bool HasPendingPlayerInvitation(this Game game, Guid userId)
    {
        return game.PendingPlayerInvitedUserIds.Contains(userId);
    }

    /// <summary>
    /// Checks if user is a pending assistant
    /// </summary>
    public static bool IsPendingAssistant(this Game game, Guid userId)
    {
        return game.PendingAssistant?.UserId == userId;
    }

    /// <summary>
    /// Convert GameRole to API string representation
    /// </summary>
    public static string ToApiString(this GameRole role) => role switch
    {
        GameRole.Master => "master",
        GameRole.Assistant => "assistant",
        GameRole.Player => "player",
        GameRole.Reader => "reader",
        _ => "unknown"
    };
}
