#pragma warning disable CS1591 // DAL entity — fields are self-documenting; see Domain.Community.Features.Achievements.AchievementType
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL для тира достижения. Тонкая запись: все, что одинаково для всех
/// тиров цепочки (Icon, Description, Metric, SortOrder) — на категории.
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
