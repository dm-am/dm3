#pragma warning disable CS1591 // DAL entity — fields are self-documenting; see Domain.Community.Features.Achievements.AchievementCategory
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL для категории достижений (цепочки тиров одной метрики).
/// SSOT для IconName, Description, Metric, SortOrder — все, что одинаково
/// для всех 4 тиров цепочки. Тиры (AchievementType) хранят только то, что
/// уникально на тир: Title, Threshold, Tier.
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
