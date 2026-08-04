using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL for an achievement category (a chain of tiers for one metric).
/// SSOT for IconName, Description, Metric, SortOrder — everything shared
/// by all 4 tiers of the chain. Tiers (AchievementType) store only what is
/// unique per tier: Title, Threshold, Tier.
/// </summary>
[Table("AchievementCategories")]
public class AchievementCategory
{
    [Key]
    public Guid AchievementCategoryId { get; set; }

    [Required]
    [MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string IconName { get; set; } = string.Empty;

    public AchievementMetric Metric { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    [InverseProperty(nameof(AchievementType.Category))]
    public virtual ICollection<AchievementType> Types { get; set; } = [];
}
