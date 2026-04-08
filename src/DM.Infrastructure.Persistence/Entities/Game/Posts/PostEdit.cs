using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game.Posts;

/// <summary>
/// DAL model for post edit history
/// </summary>
[Table("PostEdits")]
public class PostEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    [Key]
    public Guid PostEditId { get; set; }

    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Editor user identifier
    /// </summary>
    public Guid EditorUserId { get; set; }

    /// <summary>
    /// Edit timestamp (UTC)
    /// </summary>
    public DateTimeOffset EditedUtc { get; set; }

    /// <summary>
    /// Parent post
    /// </summary>
    [ForeignKey(nameof(PostId))]
    public virtual Post Post { get; set; } = null!;

    /// <summary>
    /// Editor user
    /// </summary>
    [ForeignKey(nameof(EditorUserId))]
    public virtual User Editor { get; set; } = null!;
}
