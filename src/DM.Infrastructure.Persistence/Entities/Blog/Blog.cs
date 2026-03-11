using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Blog;

/// <summary>
/// DAL model for user blog
/// </summary>
[Table("Blogs")]
public class Blog : ISoftDeletable
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    [Key]
    public Guid BlogId { get; set; }

    /// <summary>
    /// Author (Owner) identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    [MaxLength(200)]
    public string Title { get; set; } = "";

    /// <summary>
    /// Blog description
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update moment (UTC)
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }

    /// <summary>
    /// Blog status
    /// </summary>
    public ModuleStatus Status { get; set; }

    /// <summary>
    /// Premoderation status for newbie bloggers
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

    /// <summary>
    /// Premoderation mentor identifier
    /// </summary>
    public Guid? MentorId { get; set; }

    /// <summary>
    /// Visibility of draft blog (Private = only users with roles, Public = preview visible to all)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; } = DraftVisibility.Public;

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; } = true;

    /// <summary>
    /// Total publication count (denormalized for performance)
    /// </summary>
    public int PublicationCount { get; set; }

    /// <summary>
    /// Total comment count on the blog itself (denormalized for performance)
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
    /// Blog author (Owner)
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// User who deleted the blog
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Premoderation mentor
    /// </summary>
    [ForeignKey(nameof(MentorId))]
    public virtual User? Mentor { get; set; }

    /// <summary>
    /// Blog rubrics (categories)
    /// </summary>
    [InverseProperty(nameof(Rubric.Blog))]
    public virtual ICollection<Rubric> Rubrics { get; set; } = [];

    /// <summary>
    /// Blog publications
    /// </summary>
    [InverseProperty(nameof(Publication.Blog))]
    public virtual ICollection<Publication> Publications { get; set; } = [];

    /// <summary>
    /// Blog assistants
    /// </summary>
    [InverseProperty(nameof(BlogAssistant.Blog))]
    public virtual ICollection<BlogAssistant> Assistants { get; set; } = [];

    /// <summary>
    /// Blog blacklist
    /// </summary>
    [InverseProperty(nameof(BlogBlacklist.Blog))]
    public virtual ICollection<BlogBlacklist> Blacklist { get; set; } = [];

    /// <summary>
    /// Blog discussion comments (polymorphic - loaded manually via EntityId)
    /// </summary>
    [NotMapped]
    public virtual ICollection<Comment> Comments { get; set; } = [];

    /// <summary>
    /// Invitation tokens for this blog (polymorphic - uses EntityId)
    /// </summary>
    [InverseProperty(nameof(Token.Blog))]
    public virtual ICollection<Token> Tokens { get; set; } = [];

    #endregion
}
