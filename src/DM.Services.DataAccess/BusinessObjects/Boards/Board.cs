using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Boards;

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
    /// Last comment author identifier (denormalized)
    /// </summary>
    public Guid? LastCommentAuthorId { get; set; }

    /// <summary>
    /// Last comment date (denormalized, UTC)
    /// </summary>
    public DateTimeOffset? LastCommentUtc { get; set; }

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
}
