using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Administration;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.BusinessObjects.Games;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Common;

/// <summary>
/// DAL model for commentary
/// </summary>
[Table("Comments")]
public class Comment : ISoftDeletable, IEditable, IHasEditHistory<CommentEdit>
{
    /// <summary>
    /// Commentary identifier
    /// </summary>
    [Key]
    public Guid CommentId { get; set; }

    /// <summary>
    /// Forum topic identifier
    /// </summary>
    public Guid EntityId { get; set; }

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
    /// Commentary content
    /// </summary>
    public string Text { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// Parent topic
    /// </summary>
    [ForeignKey(nameof(EntityId))]
    public virtual ForumTopic Topic { get; set; }

    /// <summary>
    /// Parent game
    /// </summary>
    [ForeignKey(nameof(EntityId))]
    public virtual Game Game { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User Author { get; set; }

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User ModifiedBy { get; set; }

    /// <summary>
    /// User who deleted the comment
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User DeletedBy { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    [InverseProperty(nameof(CommentEdit.Comment))]
    public virtual ICollection<CommentEdit> Edits { get; set; }

    /// <summary>
    /// Likes
    /// </summary>
    [InverseProperty(nameof(Like.Comment))]
    public virtual ICollection<Like> Likes { get; set; }

    /// <summary>
    /// Administrative warnings
    /// </summary>
    [InverseProperty(nameof(Warning.Comment))]
    public virtual ICollection<Warning> Warnings { get; set; }
}