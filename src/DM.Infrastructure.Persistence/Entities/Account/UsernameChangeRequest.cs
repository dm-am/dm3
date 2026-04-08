using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for username change request (requires admin approval)
/// </summary>
[Table("UsernameChangeRequests")]
public class UsernameChangeRequest
{
    /// <summary>
    /// Request identifier
    /// </summary>
    [Key]
    public Guid RequestId { get; set; }

    /// <summary>
    /// User who requested the change
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Requested new username (set when user completes the change after approval)
    /// </summary>
    [MaxLength(20)]
    public string? RequestedUsername { get; set; }

    /// <summary>
    /// Reason for the change
    /// </summary>
    [MaxLength(500)]
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
    /// Approval token (generated when moderator approves, used by user to complete change)
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
    /// Resolver's comment (reason for rejection, etc.)
    /// </summary>
    [MaxLength(500)]
    public string? ResolverComment { get; set; }

    /// <summary>
    /// User who requested the change
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Moderator who resolved the request
    /// </summary>
    [ForeignKey(nameof(ResolvedByUserId))]
    public virtual User? ResolvedBy { get; set; }
}
