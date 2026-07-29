using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Achievements;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Achievements;

/// <summary>
/// Management of the achievement catalog (categories + tiers).
/// </summary>
/// <remarks>
/// Categories — PATCH only, no creation/deletion (the catalog is static and
/// tied to server metrics via <c>AchievementCategory.Metric</c>).
/// Tiers — POST/PATCH/DELETE: a SeniorModerator can add/remove a tier
/// or tune the threshold; the parent category is referenced via FK.
/// All endpoints require SeniorModerator or higher.
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Achievement Catalog")]
[RequireRole(UserRole.SeniorModerator)]
public class AchievementCatalogController : ControllerBase
{
    private readonly IAchievementCatalogApiService _achievementCatalogApiService;

    /// <inheritdoc />
    public AchievementCatalogController(IAchievementCatalogApiService achievementCatalogApiService)
    {
        _achievementCatalogApiService = achievementCatalogApiService;
    }

    /// <summary>
    /// Partially update an achievement category (Title/Description/IconName/SortOrder/IsActive).
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="request">Fields to update (null = leave untouched).</param>
    /// <response code="200">Updated.</response>
    /// <response code="400">Unknown icon or invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Category not found.</response>
    [HttpPatch("achievement-categories/{id:guid}", Name = nameof(UpdateAchievementCategory))]
    [ProducesResponseType(typeof(Envelope<AchievementCategory>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAchievementCategory(
        Guid id, [FromBody] UpdateAchievementCategoryRequest request) =>
        Ok(await _achievementCatalogApiService.UpdateCategory(id, request));

    /// <summary>Create a new tier in an existing category.</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Unknown category or invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="409">Code is already taken.</response>
    [HttpPost("achievement-types", Name = nameof(CreateAchievementType))]
    [ProducesResponseType(typeof(Envelope<AchievementType>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAchievementType([FromBody] CreateAchievementTypeRequest request) =>
        StatusCode(StatusCodes.Status201Created, await _achievementCatalogApiService.CreateType(request));

    /// <summary>Partially update a tier (Title / Threshold / Tier).</summary>
    /// <param name="id">Tier identifier.</param>
    /// <param name="request">Fields to update.</param>
    /// <response code="200">Updated.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Tier not found.</response>
    [HttpPatch("achievement-types/{id:guid}", Name = nameof(UpdateAchievementType))]
    [ProducesResponseType(typeof(Envelope<AchievementType>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAchievementType(
        Guid id, [FromBody] UpdateAchievementTypeRequest request) =>
        Ok(await _achievementCatalogApiService.UpdateType(id, request));

    /// <summary>Delete a tier from the catalog. Already earned UserAchievement records remain as history.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Tier not found.</response>
    [HttpDelete("achievement-types/{id:guid}", Name = nameof(DeleteAchievementType))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAchievementType(Guid id)
    {
        await _achievementCatalogApiService.DeleteType(id);
        return NoContent();
    }
}
