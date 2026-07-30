using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL for an achievement tier. A thin record: everything shared by all
/// tiers of the chain (Icon, Description, Metric, SortOrder) lives on the category.
/// </summary>
[Table("AchievementTypes")]
public class AchievementType
{
    [Key]
    public Guid AchievementTypeId { get; set; }

    [Required]
    [MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int Threshold { get; set; }

    public int? Tier { get; set; }

    public Guid AchievementCategoryId { get; set; }

    [ForeignKey(nameof(AchievementCategoryId))]
    public virtual AchievementCategory Category { get; set; } = null!;
}
