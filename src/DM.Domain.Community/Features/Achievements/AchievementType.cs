using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// Achievement tier within a chain. Stores only what is unique to the tier:
/// threshold, rank, name. Everything shared by the whole chain (icon,
/// description, metric, sort order, active flag) lives on <see cref="AchievementCategory"/>.
/// </summary>
public class AchievementType
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Stable tier code ("POSTS_100").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name of the tier ("Автор").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Metric threshold to unlock.</summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Visual tier (1=bronze, 2=silver, 3=gold, 4=platinum).
    /// Does not affect logic, UI only.
    /// </summary>
    public int? Tier { get; set; }

    /// <summary>Parent category (stores the metric, icon, description).</summary>
    public AchievementCategory Category { get; set; } = null!;
}

/// <summary>Request to create a new tier in the achievement catalog.</summary>
public class CreateAchievementType
{
    /// <summary>Stable code.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Tier title.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Unlock threshold.</summary>
    public int Threshold { get; set; }
    /// <summary>Tier (visual style 1-4).</summary>
    public int? Tier { get; set; }
    /// <summary>Parent category identifier.</summary>
    public Guid AchievementCategoryId { get; set; }
}

/// <summary>Request to partially update a tier.</summary>
public class UpdateAchievementType
{
    /// <summary>Identifier of the record being updated.</summary>
    public Guid Id { get; set; }
    /// <summary>New title (null = leave unchanged).</summary>
    public string? Title { get; set; }
    /// <summary>New threshold (null = leave unchanged).</summary>
    public int? Threshold { get; set; }
    /// <summary>New tier (null = leave unchanged).</summary>
    public int? Tier { get; set; }
}
