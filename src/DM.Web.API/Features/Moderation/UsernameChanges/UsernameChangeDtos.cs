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
    /// <remarks>
    /// Bounded by the column behind it, so over-length is a validation answer, not a 500.
    /// </remarks>
    [MaxLength(500, ErrorMessage = "Комментарий должен быть не более 500 символов")]
    public string? Comment { get; set; }
}
