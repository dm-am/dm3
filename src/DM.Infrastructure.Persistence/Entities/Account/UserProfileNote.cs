using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// Personal note about another user
/// </summary>
[Table("UserProfileNotes")]
public class UserProfileNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    [Key]
    public Guid UserProfileNoteId { get; set; }

    /// <summary>
    /// Owner of the note (who wrote it)
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Subject user identifier (who the note is about)
    /// </summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>
    /// Note text
    /// </summary>
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update date
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    #region Navigation properties

    /// <summary>
    /// Owner (who wrote the note)
    /// </summary>
    [ForeignKey(nameof(OwnerId))]
    public virtual User Owner { get; set; } = null!;

    /// <summary>
    /// Subject user (who the note is about)
    /// </summary>
    [ForeignKey(nameof(SubjectUserId))]
    public virtual User SubjectUser { get; set; } = null!;

    #endregion
}
