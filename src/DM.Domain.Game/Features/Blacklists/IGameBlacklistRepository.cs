using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Blacklists;

/// <summary>
/// Repository for game blacklist operations
/// </summary>
public interface IGameBlacklistRepository
{
    /// <summary>
    /// Get all blacklisted users for a game
    /// </summary>
    Task<IEnumerable<GeneralUser>> Get(Guid gameId);

    /// <summary>
    /// Add user to game blacklist
    /// </summary>
    Task<GeneralUser> Add(Guid gameId, Guid blockedUserId, Guid blockedByUserId);

    /// <summary>
    /// Copy the owner's personal blacklist into the game's, skipping anyone
    /// already on it
    /// </summary>
    /// <remarks>
    /// One statement instead of a round trip per blocked user, and the same
    /// method the blog side already has. Creating a game used to walk the list
    /// and save once per entry, after the game itself was committed.
    /// </remarks>
    /// <param name="gameId">Game identifier</param>
    /// <param name="ownerId">Owner whose personal list is copied</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>How many entries were added</returns>
    Task<int> CopyFromPersonalBlacklist(Guid gameId, Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Remove user from game blacklist
    /// </summary>
    Task Remove(Guid gameId, Guid userId);

    /// <summary>
    /// Check if user is blacklisted in game
    /// </summary>
    Task<bool> IsBlocked(Guid gameId, Guid userId, CancellationToken ct = default);
}
