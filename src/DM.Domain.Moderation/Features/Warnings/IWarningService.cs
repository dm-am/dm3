using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Service for warning management
/// </summary>
public interface IWarningService
{
    /// <summary>
    /// Get warnings for a user
    /// </summary>
    Task<IEnumerable<Warning>> GetUserWarnings(string username, CancellationToken ct = default);

    /// <summary>
    /// Get all warnings (for moderators)
    /// </summary>
    Task<IEnumerable<Warning>> GetAllWarnings(string? username = null, CancellationToken ct = default);

    /// <summary>
    /// Create a warning
    /// </summary>
    Task<Warning> CreateWarning(CreateWarning createWarning, CancellationToken ct = default);

    /// <summary>
    /// Remove (deactivate) a warning
    /// </summary>
    Task RemoveWarning(Guid warningId, CancellationToken ct = default);

    /// <summary>
    /// Get total warning points for a user
    /// </summary>
    Task<int> GetUserWarningPoints(string username, CancellationToken ct = default);

    /// <summary>
    /// Get violators: users with active warning points or an active ban (for moderators)
    /// </summary>
    Task<IEnumerable<Violator>> GetViolators(ViolatorsFilter filter = ViolatorsFilter.All, CancellationToken ct = default);
}

/// <summary>
/// DTO for creating a warning
/// </summary>
public class CreateWarning
{
    /// <summary>
    /// Target username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the warning
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Entity type
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning points (0-6, 0 = verbal warning without points)
    /// </summary>
    public int Points { get; set; } = 1;

    /// <summary>
    /// Warning reason
    /// </summary>
    public string Reason { get; set; } = "";
}
