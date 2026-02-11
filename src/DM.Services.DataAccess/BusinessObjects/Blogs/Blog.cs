using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Blogs;

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
    /// Blog owner identifier
    /// </summary>
    public Guid OwnerId { get; set; }

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
    /// Whether the blog is public (visible to all) or private (visible to owner and participants)
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; } = true;

    /// <summary>
    /// Total publication count (denormalized for performance)
    /// </summary>
    public int PublicationCount { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAtUtc { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Blog owner
    /// </summary>
    [ForeignKey(nameof(OwnerId))]
    public virtual User Owner { get; set; } = null!;

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
    /// Blog participants
    /// </summary>
    [InverseProperty(nameof(BlogParticipant.Blog))]
    public virtual ICollection<BlogParticipant> Participants { get; set; } = [];

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

    #endregion
}
