using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// DTO for username change request
/// </summary>
public class UsernameChangeRequest
{
    /// <summary>
    /// Request identifier
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// User who requested the change
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Requested new username
    /// </summary>
    public string? RequestedUsername { get; set; }

    /// <summary>
    /// Reason for the change
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
    /// Approval token
    /// </summary>
    public Guid? ApprovalToken { get; set; }

    /// <summary>
    /// When the approval token expires (UTC)
    /// </summary>
    public DateTimeOffset? ApprovalTokenExpiresUtc { get; set; }

    /// <summary>
    /// When the request was resolved (UTC)
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Who resolved the request
    /// </summary>
    public Guid? ResolvedByUserId { get; set; }

    /// <summary>
    /// Resolver's comment
    /// </summary>
    public string? ResolverComment { get; set; }

    // User info (loaded from User navigation property)

    /// <summary>
    /// User's email
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// User's current username
    /// </summary>
    public string? UserUsername { get; set; }

    /// <summary>
    /// Resolver's username (if resolved)
    /// </summary>
    public string? ResolverUsername { get; set; }
}

/// <summary>
/// Service DTO for a username change request entry (list item)
/// </summary>
public class UsernameChangeRequestEntry
{
    /// <summary>
    /// Request identifier
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// Current username
    /// </summary>
    public string CurrentUsername { get; set; } = null!;

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Requested new username (set when user completes change after approval)
    /// </summary>
    public string? RequestedUsername { get; set; }

    /// <summary>
    /// Reason for the change
    /// </summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// Request status
    /// </summary>
    public UsernameChangeRequestStatus Status { get; set; }

    /// <summary>
    /// Which deadline expired the request, set only when
    /// <see cref="Status"/> is <see cref="UsernameChangeRequestStatus.Expired"/>.
    /// </summary>
    public UsernameChangeExpiryReason? ExpiryReason { get; set; }

    /// <summary>
    /// When the request was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the approval token expires (if approved)
    /// </summary>
    public DateTimeOffset? ApprovalTokenExpiresUtc { get; set; }

    /// <summary>
    /// When the request was resolved
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Who resolved the request (username)
    /// </summary>
    public string? ResolvedByUsername { get; set; }

    /// <summary>
    /// Resolver's comment
    /// </summary>
    public string? ResolverComment { get; set; }
}

/// <summary>
/// DTO for creating a username history entry
/// </summary>
public class CreateUsernameHistory
{
    /// <summary>
    /// Record identifier
    /// </summary>
    public Guid UsernameHistoryId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Old username
    /// </summary>
    public string OldUsername { get; set; } = null!;

    /// <summary>
    /// New username
    /// </summary>
    public string NewUsername { get; set; } = null!;

    /// <summary>
    /// When the change occurred
    /// </summary>
    public DateTimeOffset ChangedUtc { get; set; }

    /// <summary>
    /// Who approved the change (null for auto-approved)
    /// </summary>
    public Guid? ApprovedById { get; set; }
}
