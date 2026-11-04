using System;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// Achievement tier within a chain. Stores only what is unique to the tier:
/// threshold, rank, name. Everything shared by the whole chain (icon,
/// description, metric, sort order, active flag) lives on <see cref="AchievementCategory"/>.
/// </summary>
public class AchievementType
{
    public Guid Id { get; set; }

    /// <summary>Stable tier code ("POSTS_100").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name of the tier ("Автор").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Metric threshold to unlock.</summary>
    public int Threshold { get; set; }

    /// <summary>
    /// The metal of the tile on <see cref="Catalogs.TierScale"/>: a chain climbs
    /// bronze, silver, gold, platinum. Does not affect logic, UI only.
    /// </summary>
    public int? Tier { get; set; }

    /// <summary>Parent category (stores the metric, icon, description).</summary>
    public AchievementCategory Category { get; set; } = null!;
}

public class CreateAchievementType
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Threshold { get; set; }
    /// <summary>Tier on <see cref="Catalogs.TierScale"/>.</summary>
    public int? Tier { get; set; }
    public Guid AchievementCategoryId { get; set; }
}

/// <summary>Request to partially update a tier; a null field is left as it is.</summary>
public class UpdateAchievementType
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public int? Threshold { get; set; }
    public int? Tier { get; set; }
}
