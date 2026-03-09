using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Game.Links;

namespace DM.Infrastructure.Persistence.Entities.CrossDomain;

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
    /// Tag group identifier
    /// </summary>
    public Guid TagGroupId { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

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
