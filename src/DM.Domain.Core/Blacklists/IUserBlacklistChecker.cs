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

    /// <summary>
    /// Of the given users, which ones have this user on their blacklist
    /// </summary>
    /// <remarks>
    /// The inverse of the reads above, and the shape a fan-out needs: a
    /// notification about one person's action is addressed to many, and asking
    /// per recipient costs one statement per recipient. A new publication
    /// notifies every subscriber of the blog, so the per-recipient read turns one
    /// event into a query for each of them.
    /// </remarks>
    /// <param name="blockedUserId">The user who might be blocked</param>
    /// <param name="ownerIds">Blacklist owners to look at</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The subset of <paramref name="ownerIds"/> that blocks the user</returns>
    Task<IReadOnlySet<Guid>> GetOwnersBlockingAsync(
        Guid blockedUserId, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default);
}
