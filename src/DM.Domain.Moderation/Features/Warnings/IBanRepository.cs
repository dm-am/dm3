using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Repository for ban operations
/// </summary>
public interface IBanRepository
{
    /// <summary>
    /// Get bans for a user (excludes removed)
    /// </summary>
    Task<IEnumerable<Ban>> GetUserBans(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get active ban for a user (if any)
    /// </summary>
    Task<Ban?> GetActiveBan(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get ban by ID
    /// </summary>
    Task<Ban?> Get(Guid banId, CancellationToken ct = default);

    /// <summary>
    /// Create a ban
    /// </summary>
    Task<Ban> Create(CreateBanEntity ban, CancellationToken ct = default);

    /// <summary>
    /// Remove (lift) a ban
    /// </summary>
    Task Remove(Guid banId, CancellationToken ct = default);

    /// <summary>
    /// Check if user is currently banned
    /// </summary>
    Task<bool> IsUserBanned(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get all active bans
    /// </summary>
    Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default);

    /// <summary>
    /// Get full ban history - active, expired and lifted - newest first, paged
    /// </summary>
    Task<(IEnumerable<Ban> Bans, int TotalCount)> GetBanHistory(int skip, int take, CancellationToken ct = default);
}
