using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Achievements;

/// <summary>
/// API service for the public read of achievements
/// </summary>
public interface IAchievementApiService
{
    /// <summary>
    /// Get achievement categories (single-metric chains)
    /// </summary>
    Task<ListEnvelope<AchievementCategory>> GetCategories();

    /// <summary>
    /// Get achievement tiers with their parent category
    /// </summary>
    Task<ListEnvelope<AchievementType>> GetTypes();

    /// <summary>
    /// Get a user's achievements, granting the ones whose threshold was crossed
    /// </summary>
    /// <param name="username">Username</param>
    Task<ListEnvelope<UserAchievement>> GetUserAchievements(string username);
}
