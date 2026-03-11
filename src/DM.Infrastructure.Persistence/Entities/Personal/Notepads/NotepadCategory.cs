using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Personal.Notepads;

/// <summary>
/// Category for organizing notepad entries
/// </summary>
[Table("NotepadCategories")]
public class NotepadCategory : ISoftDeletable
{
    /// <summary>
    /// Category identifier
    /// </summary>
    [Key]
    public Guid CategoryId { get; set; }

    /// <summary>
    /// Type of notepad this category belongs to
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
    /// Author who created this category
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Category name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Sort order within the notepad
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Is removed (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// User who deleted this category
    /// </summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>
    /// When the category was deleted
    /// </summary>
    public DateTimeOffset? DeletedUtc { get; set; }

    #region Navigation properties

    /// <summary>
    /// Author navigation property
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Entries in this category
    /// </summary>
    [InverseProperty(nameof(NotepadEntry.Category))]
    public virtual ICollection<NotepadEntry> Entries { get; set; } = [];

    #endregion
}
