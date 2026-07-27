using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>Storage for the achievement catalog (categories, tiers) and earned records.</summary>
public interface IAchievementRepository
{
    // ---- Categories ----

    /// <summary>All categories. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive, CancellationToken ct = default);
    /// <summary>Category by ID, or null.</summary>
    Task<AchievementCategory?> GetCategoryAsync(Guid id, CancellationToken ct = default);
    /// <summary>Partial category update.</summary>
    Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default);

    // ---- Tiers ----

    /// <summary>All tiers with the Category navigation included. Inactive ones (by the category flag) are hidden by default.</summary>
    Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default);
    /// <summary>Tier by ID, or null.</summary>
    Task<AchievementType?> GetTypeAsync(Guid id, CancellationToken ct = default);
    /// <summary>Tier by stable code, or null.</summary>
    Task<AchievementType?> GetTypeByCodeAsync(string code, CancellationToken ct = default);
    /// <summary>Create a new tier in an existing category.</summary>
    Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default);
    /// <summary>Partial tier update.</summary>
    Task<AchievementType> UpdateTypeAsync(UpdateAchievementType update, CancellationToken ct = default);
    /// <summary>Hard-delete a tier from the catalog.</summary>
    Task DeleteTypeAsync(Guid id, CancellationToken ct = default);

    // ---- Earnings ----

    /// <summary>All achievements of a user, newest first.</summary>
    Task<IReadOnlyCollection<UserAchievement>> GetUserAchievementsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Idempotent insert. true = actually created; false = already existed (UNIQUE constraint).</summary>
    Task<bool> TryGrantAsync(Guid userId, Guid achievementTypeId, DateTimeOffset earnedUtc, CancellationToken ct = default);
}
