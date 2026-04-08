using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Game.Links;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for tag
/// </summary>
[Table("Tags")]
public class Tag
{
    /// <summary>
    /// Tag identifier
    /// </summary>
    [Key]
    public Guid TagId { get; set; }

    /// <summary>
    /// Short numeric identifier for URLs (1, 2, 3...)
    /// </summary>
    public int ShortId { get; set; }

    /// <summary>
    /// Tag group identifier
    /// </summary>
    public Guid TagGroupId { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Description explaining what this tag means
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order within the group (lower values appear first)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Tag group
    /// </summary>
    [ForeignKey(nameof(TagGroupId))]
    public virtual TagGroup TagGroup { get; set; } = null!;

    /// <summary>
    /// Games under the tag
    /// </summary>
    [InverseProperty(nameof(GameTag.Tag))]
    public virtual ICollection<GameTag> GameTags { get; set; } = [];
}
