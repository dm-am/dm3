using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters;

/// <summary>
/// DAL model for character edit history
/// </summary>
[Table("CharacterEdits")]
public class CharacterEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    [Key]
    public Guid CharacterEditId { get; set; }

    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Editor user identifier
    /// </summary>
    public Guid EditorUserId { get; set; }

    /// <summary>
    /// Edit timestamp
    /// </summary>
    public DateTimeOffset EditedAtUtc { get; set; }

    /// <summary>
    /// Parent character
    /// </summary>
    [ForeignKey(nameof(CharacterId))]
    public virtual Character Character { get; set; } = null!;

    /// <summary>
    /// Editor user
    /// </summary>
    [ForeignKey(nameof(EditorUserId))]
    public virtual User Editor { get; set; } = null!;
}
