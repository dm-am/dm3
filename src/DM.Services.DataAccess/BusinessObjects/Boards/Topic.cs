using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Boards;

/// <summary>
/// DAL model for forum topic
/// </summary>
[Table("Topics")]
public class Topic : ISoftDeletable, IEditable, IHasEditHistory<TopicEdit>
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
    /// Author identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

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
    /// Closed topics are available in read-only mode
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Last commentary identifier
    /// For query optimisation purposes
    /// </summary>
    public Guid? LastCommentId { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// Board
    /// </summary>
    [ForeignKey(nameof(BoardId))]
    public virtual Board Board { get; set; } = null!;

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }

    /// <summary>
    /// User who deleted the topic
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Last commentary
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