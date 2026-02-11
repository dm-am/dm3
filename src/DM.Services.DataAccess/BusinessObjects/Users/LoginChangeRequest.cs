using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.DataAccess.BusinessObjects.Users;

/// <summary>
/// DAL model for login change request (requires admin approval)
/// </summary>
[Table("LoginChangeRequests")]
public class LoginChangeRequest
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
    /// Requested new login
    /// </summary>
    [MaxLength(20)]
    public string RequestedLogin { get; set; } = null!;

    /// <summary>
    /// Reason for the change
    /// </summary>
    [MaxLength(500)]
    public string Reason { get; set; } = null!;

    /// <summary>
    /// Request status
    /// </summary>
    public LoginChangeRequestStatus Status { get; set; }

    /// <summary>
    /// When the request was created (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

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
