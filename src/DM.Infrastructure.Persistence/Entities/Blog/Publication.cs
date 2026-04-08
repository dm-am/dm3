using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Blog;

/// <summary>
/// DAL model for blog publication (post)
/// </summary>
[Table("Publications")]
public class Publication : ISoftDeletable, IEditable
{
    /// <summary>
    /// Publication identifier
    /// </summary>
    [Key]
    public Guid PublicationId { get; set; }

    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Sequential publication number within the blog (for URL)
    /// </summary>
    public int PublicationNumber { get; set; }

    /// <summary>
    /// Rubric identifier (optional)
    /// </summary>
    public Guid? RubricId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Publication title
    /// </summary>
    [MaxLength(300)]
    public string Title { get; set; } = "";

    /// <summary>
    /// Publication content
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Short preview/excerpt
    /// </summary>
    [MaxLength(500)]
    public string Preview { get; set; } = "";

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor identifier
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

    /// <summary>
    /// Whether the publication is published (visible) or draft
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Publication date (when it became visible)
    /// </summary>
    public DateTimeOffset? PublishedUtc { get; set; }

    /// <summary>
    /// Whether comments are enabled for this publication
    /// </summary>
    public bool CommentsEnabled { get; set; } = true;

    /// <summary>
    /// View count
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Comment count (denormalized)
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Last comment identifier (denormalized for navigation)
    /// </summary>
    public Guid? LastCommentId { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Parent blog
    /// </summary>
    [ForeignKey(nameof(BlogId))]
    public virtual Blog Blog { get; set; } = null!;

    /// <summary>
    /// Rubric (optional)
    /// </summary>
    [ForeignKey(nameof(RubricId))]
    public virtual Rubric? Rubric { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }

    /// <summary>
    /// User who deleted the publication
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Publication comments (polymorphic - loaded manually via EntityId)
    /// </summary>
    [NotMapped]
    public virtual ICollection<Comment> Comments { get; set; } = [];

    #endregion
}
