using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Personal.Notepads;

/// <summary>
/// Notepad entry - a note in a notepad
/// </summary>
[Table("NotepadEntries")]
public class NotepadEntry : ISoftDeletable
{
    /// <summary>
    /// Entry identifier
    /// </summary>
    [Key]
    public Guid EntryId { get; set; }

    /// <summary>
    /// Type of notepad this entry belongs to
    /// </summary>
    public NotepadType NotepadType { get; set; }

    /// <summary>
    /// Container identifier (GameId for Player/Master, BlogId for Blog, UserId for User)
    /// </summary>
    public Guid ContainerId { get; set; }

    /// <summary>
    /// Owner identifier for Player notepad (PlayerId/CharacterId)
    /// Null for Master/Blog/User notepads
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// Author who created this entry
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Category identifier for organization
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Entry title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Entry content (BB-code or markdown)
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Sort order within the notepad
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update date
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }

    /// <summary>
    /// Is removed (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// User who deleted this entry
    /// </summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>
    /// When the entry was deleted
    /// </summary>
    public DateTimeOffset? DeletedUtc { get; set; }

    #region Navigation properties

    /// <summary>
    /// Author navigation property
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Category navigation property
    /// </summary>
    [ForeignKey(nameof(CategoryId))]
    public virtual NotepadCategory? Category { get; set; }

    #endregion
}
