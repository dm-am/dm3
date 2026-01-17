using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Boards;

/// <summary>
/// DAL model for forum
/// </summary>
[Table("Fora")]
public class Forum
{
    /// <summary>
    /// Forum identifier
    /// </summary>
    [Key]
    public Guid ForumId { get; set; }

    /// <summary>
    /// Forum title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Short description
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Display order number
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Role restrictions to view the forum content and the forum itself
    /// </summary>
    public ForumAccessPolicy ViewPolicy { get; set; }

    /// <summary>
    /// Role restrictions to create topics within the forum
    /// </summary>
    public ForumAccessPolicy CreateTopicPolicy { get; set; }

    /// <summary>
    /// Total topics count (denormalized)
    /// </summary>
    public int TopicsCount { get; set; }

    /// <summary>
    /// Total comments count (denormalized)
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Last comment identifier (denormalized)
    /// </summary>
    public Guid? LastCommentId { get; set; }

    /// <summary>
    /// Last comment topic identifier (denormalized)
    /// </summary>
    public Guid? LastCommentTopicId { get; set; }

    /// <summary>
    /// Last comment author identifier (denormalized)
    /// </summary>
    public Guid? LastCommentAuthorId { get; set; }

    /// <summary>
    /// Last comment date (denormalized)
    /// </summary>
    public DateTimeOffset? LastCommentDate { get; set; }

    /// <summary>
    /// Forum moderators
    /// </summary>
    [InverseProperty(nameof(ForumModerator.Forum))]
    public virtual ICollection<ForumModerator> Moderators { get; set; }

    /// <summary>
    /// Forum topics
    /// </summary>
    [InverseProperty(nameof(ForumTopic.Forum))]
    public virtual ICollection<ForumTopic> Topics { get; set; }

    /// <summary>
    /// Last comment (navigation)
    /// </summary>
    [ForeignKey(nameof(LastCommentId))]
    public virtual Comment LastComment { get; set; }

    /// <summary>
    /// Last comment author (navigation)
    /// </summary>
    [ForeignKey(nameof(LastCommentAuthorId))]
    public virtual User LastCommentAuthor { get; set; }
}