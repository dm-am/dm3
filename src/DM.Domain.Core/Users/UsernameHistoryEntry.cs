using System;

namespace DM.Domain.Core.Users;

/// <summary>
/// Service DTO for a username history entry (read-only)
/// </summary>
public class UsernameHistoryEntry
{
    /// <summary>
    /// Record identifier
    /// </summary>
    public Guid UsernameHistoryId { get; set; }

    /// <summary>
    /// Username before the change
    /// </summary>
    public string OldUsername { get; set; } = null!;

    /// <summary>
    /// Username after the change
    /// </summary>
    public string NewUsername { get; set; } = null!;

    /// <summary>
    /// When the change was made
    /// </summary>
    public DateTimeOffset ChangedUtc { get; set; }

    /// <summary>
    /// Who approved the change (username)
    /// </summary>
    public string? ApprovedByUsername { get; set; }
}
