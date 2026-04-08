using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Forum;

/// <summary>
/// DAL model for forum topic
/// </summary>
[Table("Topics")]
public class Topic : ISoftDeletable, IHasEditHistory<TopicEdit>
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    [Key]
    public Guid TopicId { get; set; }

    /// <summary>
    /// Board identifier
    /// </summary>
    public Guid BoardId { get; set; }

    /// <summary>
    /// Sequential topic number within the board (for URL)
    /// </summary>
    public int TopicNumber { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// If true, the topic will always appear on top of the forum topics list
    /// </summary>
    public bool IsAttached { get; set; }

    /// <summary>
    /// Sort order for attached topics (0 = first, null = default order by creation date)
    /// </summary>
    public int? AttachOrder { get; set; }

    /// <summary>
    /// Closed topics are available in read-only mode
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Comment count (denormalized)
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Last comment identifier (denormalized)
    /// </summary>
    public Guid? LastCommentId { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Board
    /// </summary>
    [ForeignKey(nameof(BoardId))]
    public virtual Board Board { get; set; } = null!;

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// User who deleted the topic
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Last comment
    /// </summary>
    [ForeignKey(nameof(LastCommentId))]
    public virtual Comment? LastComment { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    [InverseProperty(nameof(TopicEdit.Topic))]
    public virtual ICollection<TopicEdit> Edits { get; set; } = [];

    /// <summary>
    /// Commenaries
    /// </summary>
    [InverseProperty(nameof(Comment.Topic))]
    public virtual ICollection<Comment> Comments { get; set; } = [];
}
