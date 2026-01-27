using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Games.Posts;

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
    /// Edit timestamp
    /// </summary>
    public DateTimeOffset EditedAtUtc { get; set; }

    /// <summary>
    /// Parent post
    /// </summary>
    [ForeignKey(nameof(PostId))]
    public virtual Post Post { get; set; }

    /// <summary>
    /// Editor user
    /// </summary>
    [ForeignKey(nameof(EditorUserId))]
    public virtual User Editor { get; set; }
}
