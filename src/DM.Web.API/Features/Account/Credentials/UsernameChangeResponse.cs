using System;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Username change request status and details
/// </summary>
public class UsernameChangeResponse
{
    /// <summary>
    /// Request identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Current username
    /// </summary>
    public string CurrentUsername { get; set; } = string.Empty;

    /// <summary>
    /// Requested new username (set after user completes change)
    /// </summary>
    public string? RequestedUsername { get; set; }

    /// <summary>
    /// Reason for the change
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Request status
    /// </summary>
    public UsernameChangeRequestStatus Status { get; set; }

    /// <summary>
    /// Which deadline expired the request. Set only when the status is
    /// <see cref="UsernameChangeRequestStatus.Expired"/>: an unreviewed request
    /// and a lapsed approval share that status but mean opposite things.
    /// </summary>
    public UsernameChangeExpiryReason? ExpiryReason { get; set; }

    /// <summary>
    /// When the request was created (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the approval token expires (if approved, UTC)
    /// </summary>
    public DateTimeOffset? ApprovalTokenExpiresUtc { get; set; }

    /// <summary>
    /// When the request was resolved (UTC)
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Who resolved the request (moderator username)
    /// </summary>
    public string? ResolvedByUsername { get; set; }

    /// <summary>
    /// Resolver's comment
    /// </summary>
    public string? ResolverComment { get; set; }
}
