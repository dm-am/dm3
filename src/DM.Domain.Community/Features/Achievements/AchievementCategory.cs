using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// Achievement category = a chain of tiers for a single metric. SSOT for the icon,
/// description and ordering. One metric = one category (UNIQUE at the DB level).
/// </summary>
public class AchievementCategory
{
    /// <summary>Identifier.</summary>
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

/// <summary>Category update request. Creation from the admin UI is not expected:
/// the catalog of 13 categories is static; edits are point fixes (description, icon).</summary>
public class UpdateAchievementCategory
{
    /// <summary>Identifier of the record being updated.</summary>
    public Guid Id { get; set; }
    /// <summary>New title (null = leave unchanged).</summary>
    public string? Title { get; set; }
    /// <summary>New description (null = leave unchanged).</summary>
    public string? Description { get; set; }
    /// <summary>New icon (null = leave unchanged).</summary>
    public string? IconName { get; set; }
    /// <summary>New SortOrder (null = leave unchanged).</summary>
    public int? SortOrder { get; set; }
    /// <summary>New IsActive (null = leave unchanged).</summary>
    public bool? IsActive { get; set; }
}
