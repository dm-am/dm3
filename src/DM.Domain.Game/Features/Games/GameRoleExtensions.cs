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
    /// <param name="userId">User identifier</param>
    /// <returns>Collection of game roles</returns>
    public static IReadOnlyCollection<GameRole> GetRoles(this GameModel game, Guid userId)
    {
        var roles = new List<GameRole>();

        if (game.Author.UserId == userId)
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

        if (game.ActiveCharacterUserIds.Contains(userId))
        {
            roles.Add(GameRole.Player);
        }

        if (game.ReaderUserIds.Contains(userId))
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
    /// Checks if user has a pending invitation (player or reader)
    /// </summary>
    public static bool HasPendingInvitation(this GameModel game, Guid userId)
    {
        return game.PendingInvitedUserIds.Contains(userId);
    }

    /// <summary>
    /// Checks if user has a pending PLAYER invitation
    /// </summary>
    public static bool HasPendingPlayerInvitation(this GameModel game, Guid userId)
    {
        return game.PendingPlayerInvitedUserIds.Contains(userId);
    }

    /// <summary>
    /// Checks if user is a pending assistant
    /// </summary>
    public static bool IsPendingAssistant(this GameModel game, Guid userId)
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
