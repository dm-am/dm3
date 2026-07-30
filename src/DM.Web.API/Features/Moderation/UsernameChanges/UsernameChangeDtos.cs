using System;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Moderation.UsernameChanges;

/// <summary>
/// Username change request (moderation view)
/// </summary>
public class UsernameChangeRequest
{
    /// <summary>
    /// Request identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Current username
    /// </summary>
    public string CurrentUsername { get; set; } = null!;

    /// <summary>
    /// Requested new username (set after user completes change)
    /// </summary>
    public string? RequestedUsername { get; set; }

    /// <summary>
    /// Reason for the change request
    /// </summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// Request status
    /// </summary>
    public UsernameChangeRequestStatus Status { get; set; }

    /// <summary>
    /// When the request was created (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the approval token expires (if approved, UTC)
    /// </summary>
    public DateTimeOffset? ApprovalExpiresUtc { get; set; }

    /// <summary>
    /// When the request was resolved (UTC)
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Moderator who resolved the request
    /// </summary>
    public string? ResolvedBy { get; set; }

    /// <summary>
    /// Moderator's comment (reason for rejection/approval)
    /// </summary>
    public string? Comment { get; set; }
}

/// <summary>
/// Request to create a username change request
/// </summary>
public class CreateUsernameChangeRequest
{
    /// <summary>
    /// Reason for the change (new username is chosen after moderator approval)
    /// </summary>
    [Required(ErrorMessage = "Укажите причину смены имени")]
    [MinLength(10, ErrorMessage = "Причина должна быть не менее 10 символов")]
    [MaxLength(500, ErrorMessage = "Причина должна быть не более 500 символов")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// API DTO for resolving (approving/rejecting) a username change request
/// </summary>
public class ResolveUsernameChangeRequest
{
    /// <summary>
    /// New status (Approved or Rejected)
    /// </summary>
    public UsernameChangeRequestStatus Status { get; set; }

    /// <summary>
    /// Optional comment from moderator
    /// </summary>
    public string? Comment { get; set; }
}

/// <summary>
/// API DTO for a username history entry
/// </summary>
public class UsernameHistoryEntry
{
    /// <summary>
    /// Record identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Username before the change
    /// </summary>
    public string OldUsername { get; set; } = string.Empty;

    /// <summary>
    /// Username after the change
    /// </summary>
    public string NewUsername { get; set; } = string.Empty;

    /// <summary>
    /// When the change was made (UTC)
    /// </summary>
    public DateTimeOffset ChangedUtc { get; set; }

    /// <summary>
    /// Who approved the change (moderator username)
    /// </summary>
    public string? ApprovedByUsername { get; set; }
}
