using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// User's personal blacklist entry
/// </summary>
[Table("UserBlacklists")]
public class UserBlacklist
{
    /// <summary>
    /// Entry identifier
    /// </summary>
    [Key]
    public Guid EntryId { get; set; }

    /// <summary>
    /// Owner of the blacklist (who blocked)
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Blocked user identifier
    /// </summary>
    public Guid BlockedUserId { get; set; }

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    #region Navigation properties

    /// <summary>
    /// Owner (who blocked)
    /// </summary>
    [ForeignKey(nameof(OwnerId))]
    public virtual User Owner { get; set; } = null!;

    /// <summary>
    /// Blocked user
    /// </summary>
    [ForeignKey(nameof(BlockedUserId))]
    public virtual User BlockedUser { get; set; } = null!;

    #endregion
}
