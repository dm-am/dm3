using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Administration;

namespace DM.Services.Community.BusinessProcesses.Moderation.Warnings;

/// <summary>
/// Service for warning management
/// </summary>
public interface IWarningService
{
    /// <summary>
    /// Get warnings for a user
    /// </summary>
    Task<IEnumerable<Warning>> GetUserWarnings(string login, CancellationToken ct = default);

    /// <summary>
    /// Get all warnings (for moderators)
    /// </summary>
    Task<IEnumerable<Warning>> GetAllWarnings(string? userLogin = null, CancellationToken ct = default);

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
    Task<int> GetUserWarningPoints(string login, CancellationToken ct = default);
}

/// <summary>
/// DTO for creating a warning
/// </summary>
public class CreateWarning
{
    /// <summary>
    /// Target user login
    /// </summary>
    public string UserLogin { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the warning
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Entity type
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning points (1-3)
    /// </summary>
    public int Points { get; set; } = 1;

    /// <summary>
    /// Warning reason
    /// </summary>
    public string Reason { get; set; } = "";
}
