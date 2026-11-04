using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// Achievement category = a chain of tiers for a single metric. SSOT for the icon,
/// description and ordering. One metric = one category (UNIQUE at the DB level).
/// </summary>
public class AchievementCategory
{
    public Guid Id { get; set; }

    /// <summary>Stable code ("game_posts_authored").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name of the chain ("Игровые посты").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Metric description — what exactly is counted, what is included and what is not.
    /// Shown in the header of the achievement popover.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Icon name from the game-icons sprite (one per chain).</summary>
    public string IconName { get; set; } = string.Empty;

    /// <summary>
    /// Metric. The evaluator computes progress from it. Maps to a field of
    /// <c>GeneralUser</c> via <c>AchievementMetricResolver.GetValue</c>.
    /// </summary>
    public AchievementMetric Metric { get; set; }

    /// <summary>Ordering of chains in the UI.</summary>
    public int SortOrder { get; set; }

    /// <summary>Active = tiers can be granted and are visible in the UI.</summary>
    public bool IsActive { get; set; }
}

/// <summary>Category update request; a null field is left as it is. The catalog is
/// static: edits are point fixes (description, icon), no creation from the admin UI.</summary>
public class UpdateAchievementCategory
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
}
