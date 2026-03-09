using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blacklists;

/// <summary>
/// Repository for blog blacklist storage operations
/// </summary>
public interface IBlogBlacklistRepository
{
    /// <summary>
    /// Get all blacklisted users for a blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of blacklisted users</returns>
    Task<IEnumerable<GeneralUser>> GetBlacklist(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Check if user is already blacklisted
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if user is blacklisted</returns>
    Task<bool> IsBlocked(Guid blogId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Add user to blog blacklist
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="blockedUserId">User identifier to blacklist</param>
    /// <param name="blockedByUserId">User identifier who is blocking</param>
    /// <param name="ct">Cancellation token</param>
    Task Add(Guid blogId, Guid blockedUserId, Guid blockedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Remove user from blog blacklist
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="userId">User identifier to remove</param>
    /// <param name="ct">Cancellation token</param>
    Task Remove(Guid blogId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Cancel all pending invitations for a user in a blog (used when user is blacklisted)
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of cancelled invitation token IDs</returns>
    Task<IEnumerable<Guid>> CancelInvitationsForUser(Guid blogId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Copy user's personal blacklist to blog blacklist
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="ownerId">Owner user identifier (source of personal blacklist)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of entries copied</returns>
    Task<int> CopyFromPersonalBlacklist(Guid blogId, Guid ownerId, CancellationToken ct = default);
}
