using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Administration;

namespace DM.Services.Community.BusinessProcesses.Moderation.Warnings;

/// <summary>
/// Repository for ban operations
/// </summary>
public interface IBanRepository
{
    /// <summary>
    /// Get bans for a user
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
    Task<Ban> Create(Ban ban, CancellationToken ct = default);

    /// <summary>
    /// Remove (lift) a ban
    /// </summary>
    Task Remove(Guid banId, CancellationToken ct = default);

    /// <summary>
    /// Check if user is currently banned
    /// </summary>
    Task<bool> IsUserBanned(Guid userId, CancellationToken ct = default);
}
