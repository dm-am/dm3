using System;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Community.Achievements;

/// <summary>API DTO: achievement category (a chain of tiers for one metric).</summary>
public class AchievementCategory
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Stable code ("game_posts_authored").</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Chain title ("Игровые посты").</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Metric description (what exactly is counted).</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Icon name from the game-icons sprite.</summary>
    public string IconName { get; set; } = string.Empty;
    /// <summary>Metric.</summary>
    public AchievementMetric Metric { get; set; }
    /// <summary>Ordering of chains in the UI.</summary>
    public int SortOrder { get; set; }
    /// <summary>Whether the category is active.</summary>
    public bool IsActive { get; set; }
}

/// <summary>API DTO: achievement tier. The parent category is included in the response.</summary>
public class AchievementType
{
    /// <summary>Tier identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Stable code ("POSTS_100").</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Tier title ("Автор").</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Unlock threshold.</summary>
    public int Threshold { get; set; }
    /// <summary>Visual tier (1-4).</summary>
    public int? Tier { get; set; }
    /// <summary>Parent category (SSOT for the chain icon, metric, description).</summary>
    public AchievementCategory Category { get; set; } = null!;
}

/// <summary>API DTO: the fact that a user earned an achievement.</summary>
public class UserAchievement
{
    /// <summary>Earned record identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Achievement type.</summary>
    public AchievementType Type { get; set; } = null!;
    /// <summary>Moment the threshold was crossed (UTC).</summary>
    public DateTimeOffset EarnedUtc { get; set; }
}

/// <summary>Category update request. Creation is not supported: the catalog is static.</summary>
public class UpdateAchievementCategoryRequest
{
    /// <summary>New title.</summary>
    public string? Title { get; set; }
    /// <summary>New description.</summary>
    public string? Description { get; set; }
    /// <summary>New icon.</summary>
    public string? IconName { get; set; }
    /// <summary>New SortOrder.</summary>
    public int? SortOrder { get; set; }
    /// <summary>New IsActive.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Request to create a new tier.</summary>
public class CreateAchievementTypeRequest
{
    /// <summary>Stable code (unique).</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Tier title.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Unlock threshold.</summary>
    public int Threshold { get; set; }
    /// <summary>Visual tier (1-4).</summary>
    public int? Tier { get; set; }
    /// <summary>Parent category identifier.</summary>
    public Guid AchievementCategoryId { get; set; }
}

/// <summary>Request to partially update a tier.</summary>
public class UpdateAchievementTypeRequest
{
    /// <summary>New title.</summary>
    public string? Title { get; set; }
    /// <summary>New threshold.</summary>
    public int? Threshold { get; set; }
    /// <summary>New tier.</summary>
    public int? Tier { get; set; }
}
