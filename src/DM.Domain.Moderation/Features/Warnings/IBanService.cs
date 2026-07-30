using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Service for ban management
/// </summary>
public interface IBanService
{
    /// <summary>
    /// Get bans for a user
    /// </summary>
    Task<IEnumerable<Ban>> GetUserBans(string username, CancellationToken ct = default);

    /// <summary>
    /// Get active ban for a user (if any)
    /// </summary>
    Task<Ban?> GetActiveBan(string username, CancellationToken ct = default);

    /// <summary>
    /// Get all active bans (for moderators)
    /// </summary>
    Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default);

    /// <summary>
    /// Get full ban history - active, expired and lifted - newest first, paged (for moderators)
    /// </summary>
    Task<(IEnumerable<Ban> Bans, int TotalCount)> GetBanHistory(int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Create a ban (senior moderators; users can create voluntary self-bans)
    /// </summary>
    Task<Ban> CreateBan(CreateBan createBan, CancellationToken ct = default);

    /// <summary>
    /// Lift (cancel) a ban early (senior moderators; permanent bans - admins only)
    /// </summary>
    Task LiftBan(Guid banId, string? reason = null, CancellationToken ct = default);
}

/// <summary>
/// DTO for creating a ban
/// </summary>
public class CreateBan
{
    /// <summary>
    /// Target username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Ban expiration time (null for permanent)
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Ban duration in hours (alternative to ExpiresUtc)
    /// </summary>
    public int? DurationHours { get; set; }

    /// <summary>
    /// Ban comment/reason
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// Ban access restriction scope ("Тип бана", 4.2.4.2):
    /// <see cref="AccessPolicy.DemocraticBan"/> (read-only) or
    /// <see cref="AccessPolicy.FullBan"/> (everything blocked). The service
    /// coerces any other value to FullBan. Defaults to FullBan so that
    /// existing callers (e.g. ticket resolution) keep the full restriction.
    /// </summary>
    public AccessPolicy AccessRestrictionPolicy { get; set; } = AccessPolicy.FullBan;

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }
}
