using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Achievements;

/// <summary>
/// Public read of the achievement catalog and users' earned
/// records. The lazy-eval engine runs on every per-user GET:
/// types whose threshold was crossed since the previous call are INSERTed
/// and immediately included in the response.
/// </summary>
/// <remarks>
/// Catalog editing lives in <c>v1/moderation/achievement-categories</c>
/// and <c>v1/moderation/achievement-types</c> (admin only).
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Achievements")]
public class AchievementController : ControllerBase
{
    private readonly IAchievementApiService _achievementApiService;

    /// <inheritdoc />
    public AchievementController(IAchievementApiService achievementApiService)
    {
        _achievementApiService = achievementApiService;
    }

    /// <summary>List of achievement categories (single-metric chains).</summary>
    /// <remarks>Inactive ones are hidden. SortOrder and description are the SSOT for the FE.</remarks>
    /// <response code="200">List of categories.</response>
    [HttpGet("achievement-categories", Name = nameof(GetAchievementCategories))]
    [ProducesResponseType(typeof(ListEnvelope<AchievementCategory>), StatusCodes.Status200OK)]
    // A catalogue: the service is called without an identity, so every caller
    // gets the same bytes. Same policy as /v1/games/tags.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetAchievementCategories() =>
        Ok(await _achievementApiService.GetCategories());

    /// <summary>List of achievement tiers (active only, with the parent category).</summary>
    /// <response code="200">List of tiers.</response>
    [HttpGet("achievement-types", Name = nameof(GetAchievementTypes))]
    [ProducesResponseType(typeof(ListEnvelope<AchievementType>), StatusCodes.Status200OK)]
    // A catalogue: the service is called without an identity, so every caller
    // gets the same bytes. Same policy as /v1/games/tags.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetAchievementTypes() =>
        Ok(await _achievementApiService.GetTypes());

    /// <summary>
    /// A user's achievements. Lazy-eval: on every request, not-yet-earned
    /// types are checked against their thresholds and granted automatically.
    /// </summary>
    /// <param name="username">Username.</param>
    /// <response code="200">List of achievements (newest first).</response>
    /// <response code="404">User not found.</response>
    [HttpGet("users/{username}/achievements", Name = nameof(GetUserAchievements))]
    [ProducesResponseType(typeof(ListEnvelope<UserAchievement>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAchievements(string username) =>
        Ok(await _achievementApiService.GetUserAchievements(username));
}
