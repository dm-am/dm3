using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>Service for the achievement catalog and the lazy-eval engine.</summary>
public interface IAchievementService
{
    // ---- Category catalog ----

    /// <summary>List of categories. Inactive ones are hidden by default.</summary>
    Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive = false, CancellationToken ct = default);

    /// <summary>Partially update a category. Creation/deletion is not supported — the catalog is static.</summary>
    Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default);

    // ---- Tier catalog ----

    /// <summary>Get the list of tiers. The parent category is included in the navigation.</summary>
    Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive = false, CancellationToken ct = default);

    /// <summary>Create a tier in an existing category. Validates Code uniqueness.</summary>
    Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default);

    /// <summary>Partially update a tier (Title/Threshold/Tier).</summary>
    Task<AchievementType> UpdateTypeAsync(UpdateAchievementType update, CancellationToken ct = default);

    /// <summary>Hard-delete a tier from the catalog. Related UserAchievement records remain as history.</summary>
    Task DeleteTypeAsync(Guid id, CancellationToken ct = default);

    // ---- User earnings ----

    /// <summary>
    /// Get a user's achievements with lazy-eval: for every active
    /// tier not yet earned, check the threshold. Newly earned ones
    /// are INSERTed and added to the result within the same request.
    /// </summary>
    Task<IReadOnlyCollection<UserAchievement>> GetUserAchievementsAsync(string username, CancellationToken ct = default);
}
