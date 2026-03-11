using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// Moderator note about a user (visible only to moderators)
/// </summary>
[Table("ModeratedProfileNotes")]
public class ModeratedProfileNote : IRemovable
{
    /// <summary>
    /// Note identifier
    /// </summary>
    [Key]
    public Guid ModeratedProfileNoteId { get; set; }

    /// <summary>
    /// User this note is about
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Moderator who created this note
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Note text
    /// </summary>
    [MaxLength(4000)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification date (UTC)
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }

    /// <summary>
    /// Whether the note is soft-deleted
    /// </summary>
    public bool IsRemoved { get; set; }

    #region Navigation properties

    /// <summary>
    /// User this note is about
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Moderator who created this note
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    #endregion
}
