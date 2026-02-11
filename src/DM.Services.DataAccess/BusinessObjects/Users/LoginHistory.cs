using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Services.DataAccess.BusinessObjects.Users;

/// <summary>
/// DAL model for login change history. Old logins are permanently reserved.
/// </summary>
[Table("LoginHistories")]
public class LoginHistory
{
    /// <summary>
    /// Record identifier
    /// </summary>
    [Key]
    public Guid LoginHistoryId { get; set; }

    /// <summary>
    /// User who changed their login
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Login BEFORE the change. This login is permanently reserved and cannot be reused.
    /// </summary>
    [MaxLength(20)]
    public string OldLogin { get; set; } = null!;

    /// <summary>
    /// Login AFTER the change
    /// </summary>
    [MaxLength(20)]
    public string NewLogin { get; set; } = null!;

    /// <summary>
    /// When the change was made (UTC)
    /// </summary>
    public DateTimeOffset ChangedUtc { get; set; }

    /// <summary>
    /// Who approved the change (Admin/SeniorMod). Null for automatic changes.
    /// </summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>
    /// User who changed login
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Moderator who approved the change
    /// </summary>
    [ForeignKey(nameof(ApprovedByUserId))]
    public virtual User? ApprovedBy { get; set; }
}
