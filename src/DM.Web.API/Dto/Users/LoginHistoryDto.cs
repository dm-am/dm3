using System;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// API DTO for a login history entry
/// </summary>
public class LoginHistoryDto
{
    /// <summary>
    /// Record identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Login before the change
    /// </summary>
    public string OldLogin { get; set; } = null!;

    /// <summary>
    /// Login after the change
    /// </summary>
    public string NewLogin { get; set; } = null!;

    /// <summary>
    /// When the change was made (UTC)
    /// </summary>
    public DateTimeOffset ChangedUtc { get; set; }

    /// <summary>
    /// Who approved the change (moderator login)
    /// </summary>
    public string? ApprovedByLogin { get; set; }
}
