using System;
using System.Threading.Tasks;
using DM.Web.API.Features.Community.Achievements;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Achievements;

/// <summary>
/// API service for the achievement catalog (categories + tiers)
/// </summary>
public interface IAchievementCatalogApiService
{
    /// <summary>
    /// Partially update an achievement category
    /// </summary>
    /// <param name="id">Category identifier</param>
    /// <param name="request">Fields to update</param>
    Task<Envelope<AchievementCategory>> UpdateCategory(Guid id, UpdateAchievementCategoryRequest request);

    /// <summary>
    /// Create a new tier in an existing category
    /// </summary>
    Task<Envelope<AchievementType>> CreateType(CreateAchievementTypeRequest request);

    /// <summary>
    /// Partially update a tier
    /// </summary>
    /// <param name="id">Tier identifier</param>
    /// <param name="request">Fields to update</param>
    Task<Envelope<AchievementType>> UpdateType(Guid id, UpdateAchievementTypeRequest request);

    /// <summary>
    /// Delete a tier from the catalog, keeping earned records as history
    /// </summary>
    /// <param name="id">Tier identifier</param>
    Task DeleteType(Guid id);
}
