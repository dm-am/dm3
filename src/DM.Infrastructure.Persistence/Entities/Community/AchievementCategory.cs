using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Community.Features.Catalogs;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL for an achievement category (a chain of tiers for one metric).
/// The source of IconName, Description, Metric, SortOrder — everything shared
/// by all 4 tiers of the chain. Tiers (AchievementType) store only what is
/// unique per tier: Title, Threshold, Tier.
/// </summary>
[Table("AchievementCategories")]
public class AchievementCategory
{
    [Key]
    public Guid AchievementCategoryId { get; set; }

    [Required]
    [MaxLength(CatalogFieldLimits.CodeMaxLength)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(CatalogFieldLimits.TitleMaxLength)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(CatalogFieldLimits.DescriptionMaxLength)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(CatalogFieldLimits.IconNameMaxLength)]
    public string IconName { get; set; } = string.Empty;

    public AchievementMetric Metric { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    [InverseProperty(nameof(AchievementType.Category))]
    public virtual ICollection<AchievementType> Types { get; set; } = [];
}
