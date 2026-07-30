using DM.Domain.Game.Features.Games;
using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Resolves the highest role a user has in a game
/// </summary>
public interface IGameRoleResolver
{
    /// <summary>
    /// Get user's highest role in a game (loads game data and subscriptions)
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Highest role the user has in the game</returns>
    Task<GameRole> GetRoleAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get user's highest role from an already-loaded game DTO
    /// </summary>
    /// <remarks>
    /// Reader detection reads <see cref="Game.IsViewerSubscriber" />, which the
    /// repository fills for the user the game was read for. Pass that same user;
    /// use <see cref="GetRoleAsync" /> to ask about anybody else.
    /// </remarks>
    /// <param name="game">Game DTO with participation data</param>
    /// <param name="userId">User identifier the game was read for</param>
    /// <returns>Highest role the user has in the game</returns>
    GameRole GetRole(Game game, Guid userId);
}
