using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Blacklists;

/// <summary>
/// Read-only interface for checking user blacklist status.
/// Used for cross-module blacklist checks (Blog, Forum, Game, Messaging).
/// </summary>
/// <remarks>
/// This interface is in Domain.Core to allow modules to check if a user
/// is blocked without creating a dependency on Domain.Personal.
/// Full blacklist management (add/remove) is in IUserBlacklistService.
/// </remarks>
public interface IUserBlacklistChecker
{
    /// <summary>
    /// Check if a user is blocked by another user
    /// </summary>
    /// <param name="ownerId">The user who owns the blacklist</param>
    /// <param name="blockedUserId">The user to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if blockedUserId is in ownerId's blacklist</returns>
    Task<bool> IsBlockedAsync(Guid ownerId, Guid blockedUserId, CancellationToken ct = default);

    /// <summary>
    /// Get IDs of users blocked by a specific user
    /// </summary>
    /// <param name="ownerId">The user who owns the blacklist</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of blocked user IDs</returns>
    Task<IEnumerable<Guid>> GetBlockedUserIdsAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Get IDs of users blocked by a specific user, only if the specified flag is enabled.
    /// Returns empty collection if flag is not set.
    /// </summary>
    /// <param name="ownerId">The user who owns the blacklist</param>
    /// <param name="flag">The blacklist setting flag to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of blocked user IDs if flag is enabled, empty otherwise</returns>
    Task<IReadOnlySet<Guid>> GetBlockedUserIdsIfFlagEnabledAsync(Guid ownerId, UserBlacklistSettings flag, CancellationToken ct = default);
}
