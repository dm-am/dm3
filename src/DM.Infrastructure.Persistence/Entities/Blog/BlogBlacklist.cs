using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Blog;

/// <summary>
/// DAL model for blog blacklist entry (user banned from commenting in a blog)
/// </summary>
[Table("BlogBlacklists")]
public class BlogBlacklist
{
    [Key]
    public Guid EntryId { get; set; }

    public Guid BlogId { get; set; }

    /// <summary>
    /// Blocked user identifier
    /// </summary>
    public Guid BlockedUserId { get; set; }

    /// <summary>
    /// When user was blocked
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Who blocked the user
    /// </summary>
    public Guid BlockedByUserId { get; set; }

    #region Navigation Properties

    [ForeignKey(nameof(BlogId))]
    public virtual Blog Blog { get; set; } = null!;

    [ForeignKey(nameof(BlockedUserId))]
    public virtual User BlockedUser { get; set; } = null!;

    [ForeignKey(nameof(BlockedByUserId))]
    public virtual User BlockedBy { get; set; } = null!;

    #endregion
}
