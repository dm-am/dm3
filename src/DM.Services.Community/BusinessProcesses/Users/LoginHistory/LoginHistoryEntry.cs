using System;

namespace DM.Services.Community.BusinessProcesses.Users.LoginHistory;

/// <summary>
/// Service DTO for a login history entry
/// </summary>
public class LoginHistoryEntry
{
    /// <summary>
    /// Record identifier
    /// </summary>
    public Guid LoginHistoryId { get; set; }

    /// <summary>
    /// Login before the change
    /// </summary>
    public string OldLogin { get; set; } = null!;

    /// <summary>
    /// Login after the change
    /// </summary>
    public string NewLogin { get; set; } = null!;

    /// <summary>
    /// When the change was made
    /// </summary>
    public DateTimeOffset ChangedUtc { get; set; }

    /// <summary>
    /// Who approved the change (login)
    /// </summary>
    public string? ApprovedByLogin { get; set; }
}
