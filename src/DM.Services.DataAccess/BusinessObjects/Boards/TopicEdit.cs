using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Boards;

/// <summary>
/// DAL model for topic edit history
/// </summary>
[Table("TopicEdits")]
public class TopicEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    [Key]
    public Guid TopicEditId { get; set; }

    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Editor user identifier
    /// </summary>
    public Guid EditorUserId { get; set; }

    /// <summary>
    /// Edit timestamp
    /// </summary>
    public DateTimeOffset EditedAtUtc { get; set; }

    /// <summary>
    /// Parent topic
    /// </summary>
    [ForeignKey(nameof(TopicId))]
    public virtual Topic Topic { get; set; } = null!;

    /// <summary>
    /// Editor user
    /// </summary>
    [ForeignKey(nameof(EditorUserId))]
    public virtual User Editor { get; set; } = null!;
}
