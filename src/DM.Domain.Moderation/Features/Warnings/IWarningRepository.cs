using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Repository for warning operations
/// </summary>
public interface IWarningRepository
{
    /// <summary>
    /// Get warnings for a user (excludes removed)
    /// </summary>
    Task<IEnumerable<Warning>> GetUserWarnings(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get warning by ID
    /// </summary>
    Task<Warning?> Get(Guid warningId, CancellationToken ct = default);

    /// <summary>
    /// Create a warning
    /// </summary>
    Task<Warning> Create(CreateWarningEntity warning, CancellationToken ct = default);

    /// <summary>
    /// Remove (soft delete) a warning
    /// </summary>
    Task Remove(Guid warningId, CancellationToken ct = default);

    /// <summary>
    /// Get total warning points for a user (non-removed warnings)
    /// </summary>
    Task<int> GetUserWarningPoints(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get per-user aggregates of active warning points (only users with points > 0)
    /// </summary>
    Task<IEnumerable<UserWarningSummary>> GetActiveWarningSummaries(CancellationToken ct = default);
}
