using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for tag group
/// </summary>
[Table("TagGroups")]
public class TagGroup
{
    /// <summary>
    /// Tag group identifier
    /// </summary>
    [Key]
    public Guid TagGroupId { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Description explaining the purpose of tags in this group
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for display (lower values appear first)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Tags under the group
    /// </summary>
    [InverseProperty(nameof(Tag.TagGroup))]
    public virtual ICollection<Tag> Tags { get; set; } = [];
}
