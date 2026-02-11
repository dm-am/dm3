using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Common;

/// <summary>
/// DAL model for comment edit history
/// </summary>
[Table("CommentEdits")]
public class CommentEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    [Key]
    public Guid CommentEditId { get; set; }

    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Editor user identifier
    /// </summary>
    public Guid EditorUserId { get; set; }

    /// <summary>
    /// Edit timestamp
    /// </summary>
    public DateTimeOffset EditedAtUtc { get; set; }

    /// <summary>
    /// Parent comment
    /// </summary>
    [ForeignKey(nameof(CommentId))]
    public virtual Comment Comment { get; set; } = null!;

    /// <summary>
    /// Editor user
    /// </summary>
    [ForeignKey(nameof(EditorUserId))]
    public virtual User Editor { get; set; } = null!;
}
