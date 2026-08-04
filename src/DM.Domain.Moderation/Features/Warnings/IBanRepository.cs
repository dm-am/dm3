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
    /// Get every ban a user ever received - running, expired and lifted. A lifted
    /// ban belongs in the history: filtering it out made it disappear from the
    /// profile as if it had never been issued.
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
    /// Lift a ban early. The row stays; only the audit below is written.
    /// </summary>
    /// <param name="banId">Ban identifier</param>
    /// <param name="liftedByUserId">Moderator lifting the ban</param>
    /// <param name="liftedUtc">When it was lifted</param>
    /// <param name="reason">Why it was lifted</param>
    /// <param name="ct">Cancellation token</param>
    Task Lift(Guid banId, Guid liftedByUserId, DateTimeOffset liftedUtc, string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// Get all active bans
    /// </summary>
    Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default);

    /// <summary>
    /// Get full ban history - active, expired and lifted - newest first, paged
    /// </summary>
    Task<(IEnumerable<Ban> Bans, int TotalCount)> GetBanHistory(int skip, int take, CancellationToken ct = default);
}
