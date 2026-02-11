using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Blogs;

/// <summary>
/// DAL model for blog blacklist entry (user banned from commenting in a blog)
/// </summary>
[Table("BlogBlacklists")]
public class BlogBlacklist
{
    /// <summary>
    /// Entry identifier
    /// </summary>
    [Key]
    public Guid EntryId { get; set; }

    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Blocked user identifier
    /// </summary>
    public Guid BlockedUserId { get; set; }

    /// <summary>
    /// Reason for blocking
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    /// When user was blocked
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Who blocked the user
    /// </summary>
    public Guid BlockedByUserId { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Blog
    /// </summary>
    [ForeignKey(nameof(BlogId))]
    public virtual Blog Blog { get; set; } = null!;

    /// <summary>
    /// Blocked user
    /// </summary>
    [ForeignKey(nameof(BlockedUserId))]
    public virtual User BlockedUser { get; set; } = null!;

    /// <summary>
    /// User who blocked
    /// </summary>
    [ForeignKey(nameof(BlockedByUserId))]
    public virtual User BlockedBy { get; set; } = null!;

    #endregion
}
