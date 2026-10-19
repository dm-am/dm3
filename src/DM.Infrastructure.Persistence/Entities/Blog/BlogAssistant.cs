using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Blog;

/// <summary>
/// DAL model for blog assistant
/// </summary>
/// <remarks>
/// Unified with GameAssistant structure.
/// Only stores assistants - readers are in Subscriptions table.
/// </remarks>
[Table("BlogAssistants")]
public class BlogAssistant
{
    /// <summary>
    /// Entry identifier
    /// </summary>
    [Key]
    public Guid BlogAssistantId { get; set; }

    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// When user became assistant
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }

    #region Navigation Properties

    [ForeignKey(nameof(BlogId))]
    public virtual Blog Blog { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    #endregion
}
