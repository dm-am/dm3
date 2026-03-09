using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for username change history. Old usernames are permanently reserved.
/// </summary>
[Table("UsernameHistories")]
public class UsernameHistory
{
    /// <summary>
    /// Record identifier
    /// </summary>
    [Key]
    public Guid UsernameHistoryId { get; set; }

    /// <summary>
    /// User who changed their username
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Username BEFORE the change. This username is permanently reserved and cannot be reused.
    /// </summary>
    [MaxLength(20)]
    public string OldUsername { get; set; } = null!;

    /// <summary>
    /// Username AFTER the change
    /// </summary>
    [MaxLength(20)]
    public string NewUsername { get; set; } = null!;

    /// <summary>
    /// When the change was made (UTC)
    /// </summary>
    public DateTimeOffset ChangedUtc { get; set; }

    /// <summary>
    /// Who approved the change (Admin/SeniorMod). Null for automatic changes.
    /// </summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>
    /// User who changed username
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Moderator who approved the change
    /// </summary>
    [ForeignKey(nameof(ApprovedByUserId))]
    public virtual User? ApprovedBy { get; set; }
}
