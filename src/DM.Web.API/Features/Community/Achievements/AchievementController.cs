using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.Achievements;
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
    private readonly IAchievementService _achievementService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AchievementController(IAchievementService achievementService, IMapper mapper)
    {
        _achievementService = achievementService;
        _mapper = mapper;
    }

    /// <summary>List of achievement categories (single-metric chains).</summary>
    /// <remarks>Inactive ones are hidden. SortOrder and description are the SSOT for the FE.</remarks>
    /// <response code="200">List of categories.</response>
    [HttpGet("achievement-categories", Name = nameof(GetAchievementCategories))]
    [ProducesResponseType(typeof(ListEnvelope<AchievementCategory>), StatusCodes.Status200OK)]
    // A catalogue: the service is called without an identity, so every caller
    // gets the same bytes. Same policy as /v1/games/tags.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetAchievementCategories()
    {
        var categories = await _achievementService.GetCategoriesAsync();
        var items = categories.Select(_mapper.Map<AchievementCategory>);
        return Ok(new ListEnvelope<AchievementCategory>(items, null));
    }

    /// <summary>List of achievement tiers (active only, with the parent category).</summary>
    /// <response code="200">List of tiers.</response>
    [HttpGet("achievement-types", Name = nameof(GetAchievementTypes))]
    [ProducesResponseType(typeof(ListEnvelope<AchievementType>), StatusCodes.Status200OK)]
    // A catalogue: the service is called without an identity, so every caller
    // gets the same bytes. Same policy as /v1/games/tags.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetAchievementTypes()
    {
        var types = await _achievementService.GetTypesAsync();
        var items = types.Select(_mapper.Map<AchievementType>);
        return Ok(new ListEnvelope<AchievementType>(items, null));
    }

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
    public async Task<IActionResult> GetUserAchievements(string username)
    {
        var list = await _achievementService.GetUserAchievementsAsync(username);
        var items = list.Select(_mapper.Map<UserAchievement>);
        return Ok(new ListEnvelope<UserAchievement>(items, null));
    }
}
