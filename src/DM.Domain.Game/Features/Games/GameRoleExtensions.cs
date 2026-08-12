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
    /// curating the game. The four are listed by name rather than as everything
    /// that is not a Reader, so that a role added to <see cref="GameRole" />
    /// later does not widen the ban exemption by itself; the one already in the
    /// enum and not yet resolved anywhere is <see cref="GameRole.Applicant" />.
    /// A plain Reader is out because a subscription is
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
        return roles.Any(r =>
            r is GameRole.Master or GameRole.Assistant or GameRole.Mentor or GameRole.Player);
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
    /// Whether the game's owner put this user on its blacklist.
    /// </summary>
    /// <remarks>
    /// The blacklist closes writing and leaves reading alone: the game is public
    /// to everybody else, so hiding it from one person would promise a privacy it
    /// does not have. Every refusal built on this predicate is therefore on a
    /// write intention, and the storage filters that answer the game and room
    /// lists do not consult the list at all.
    ///
    /// One spelling on purpose. The question was written out six times as an
    /// inline Any over BlacklistedUsers, and a rule this short is exactly the kind
    /// whose copies drift the moment one of them is edited.
    /// </remarks>
    public static bool IsBlacklisted(this Game game, Guid userId)
    {
        return game.BlacklistedUsers.Any(b => b.UserId == userId);
    }

    /// <summary>
    /// Convert GameRole to API string representation.
    /// </summary>
    /// <remarks>
    /// Every member, and no default arm on purpose. Four of the seven were listed
    /// and the rest collapsed into "unknown", so the mentor the roster query
    /// returns reached the client under a role its union type does not contain.
    /// Without the arm the compiler names an unrendered member (CS8509 is
    /// deliberately not silenced, see Directory.Build.props).
    /// </remarks>
    public static string ToApiString(this GameRole role) => role switch
    {
        GameRole.Master => "master",
        GameRole.Assistant => "assistant",
        GameRole.Mentor => "mentor",
        GameRole.Player => "player",
        GameRole.Applicant => "applicant",
        GameRole.Reader => "reader",
        GameRole.None => "none"
    };

    /// <summary>
    /// Parse the API string representation back into a role. Derived from
    /// <see cref="ToApiString" /> rather than spelled out again: the role filter
    /// kept its own literals, three of which the renderer never produced, so those
    /// filters answered empty for every game.
    /// </summary>
    public static bool TryParseApiString(string? value, out GameRole role)
    {
        foreach (var candidate in Enum.GetValues<GameRole>())
        {
            if (string.Equals(candidate.ToApiString(), value, StringComparison.OrdinalIgnoreCase))
            {
                role = candidate;
                return true;
            }
        }

        role = GameRole.None;
        return false;
    }
}
