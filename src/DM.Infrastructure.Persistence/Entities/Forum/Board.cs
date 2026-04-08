using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Forum;

/// <summary>
/// DAL model for board (forum section)
/// </summary>
[Table("Boards")]
public class Board
{
    /// <summary>
    /// Board identifier
    /// </summary>
    [Key]
    public Guid BoardId { get; set; }

    /// <summary>
    /// Board title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// URL-friendly alias (ASCII, lowercase, hyphens)
    /// </summary>
    public string Alias { get; set; } = null!;

    /// <summary>
    /// Short description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Display order number
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Role restrictions to view the board content and the board itself
    /// </summary>
    public BoardAccessPolicy ViewPolicy { get; set; }

    /// <summary>
    /// Role restrictions to create topics within the board
    /// </summary>
    public BoardAccessPolicy CreateTopicPolicy { get; set; }

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
    /// Last comment topic title (denormalized)
    /// </summary>
    public string? LastCommentTopicTitle { get; set; }

    /// <summary>
    /// Last comment topic number (denormalized)
    /// </summary>
    public int? LastCommentTopicNumber { get; set; }

    /// <summary>
    /// Last comment author identifier (denormalized)
    /// </summary>
    public Guid? LastCommentAuthorId { get; set; }

    /// <summary>
    /// Last comment date (denormalized, UTC)
    /// </summary>
    public DateTimeOffset? LastCommentUtc { get; set; }

    /// <summary>
    /// Last created topic identifier (denormalized)
    /// </summary>
    public Guid? LastTopicId { get; set; }

    /// <summary>
    /// Last created topic number (denormalized)
    /// </summary>
    public int? LastTopicNumber { get; set; }

    /// <summary>
    /// Last created topic title (denormalized)
    /// </summary>
    public string? LastTopicTitle { get; set; }

    /// <summary>
    /// Last created topic author identifier (denormalized)
    /// </summary>
    public Guid? LastTopicAuthorId { get; set; }

    /// <summary>
    /// Last created topic date (denormalized, UTC)
    /// </summary>
    public DateTimeOffset? LastTopicCreatedUtc { get; set; }

    /// <summary>
    /// Board moderators
    /// </summary>
    [InverseProperty(nameof(BoardModerator.Board))]
    public virtual ICollection<BoardModerator> Moderators { get; set; } = [];

    /// <summary>
    /// Board topics
    /// </summary>
    [InverseProperty(nameof(Topic.Board))]
    public virtual ICollection<Topic> Topics { get; set; } = [];

    /// <summary>
    /// Last comment (navigation)
    /// </summary>
    [ForeignKey(nameof(LastCommentId))]
    public virtual Comment? LastComment { get; set; }

    /// <summary>
    /// Last comment author (navigation)
    /// </summary>
    [ForeignKey(nameof(LastCommentAuthorId))]
    public virtual User? LastCommentAuthor { get; set; }

    /// <summary>
    /// Last created topic (navigation)
    /// </summary>
    [ForeignKey(nameof(LastTopicId))]
    public virtual Topic? LastTopic { get; set; }

    /// <summary>
    /// Last created topic author (navigation)
    /// </summary>
    [ForeignKey(nameof(LastTopicAuthorId))]
    public virtual User? LastTopicAuthor { get; set; }
}
