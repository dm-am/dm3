using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>Storage for the achievement catalog (categories, tiers) and earned records.</summary>
public interface IAchievementRepository
{
    /// <summary>All categories. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive, CancellationToken ct = default);
    Task<AchievementCategory?> GetCategoryAsync(Guid id, CancellationToken ct = default);
    Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default);

    /// <summary>All tiers with the Category navigation included. Inactive ones (by the category flag) are hidden by default.</summary>
    Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive, CancellationToken ct = default);
    Task<AchievementType?> GetTypeAsync(Guid id, CancellationToken ct = default);
    Task<AchievementType?> GetTypeByCodeAsync(string code, CancellationToken ct = default);
    Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default);
    Task<AchievementType> UpdateTypeAsync(UpdateAchievementType update, CancellationToken ct = default);
    /// <summary>Hard-delete a tier from the catalog.</summary>
    Task DeleteTypeAsync(Guid id, CancellationToken ct = default);

    /// <summary>All achievements of a user, newest first.</summary>
    Task<IReadOnlyCollection<UserAchievement>> GetUserAchievementsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Idempotent insert. true = actually created; false = already existed (UNIQUE constraint).</summary>
    Task<bool> TryGrantAsync(Guid userId, Guid achievementTypeId, DateTimeOffset earnedUtc, CancellationToken ct = default);
}
