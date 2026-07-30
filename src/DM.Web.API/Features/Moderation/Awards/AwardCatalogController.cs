using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Awards;

/// <summary>
/// CRUD for the award catalog and contest series.
/// </summary>
/// <remarks>
/// Award types (`AwardType`) — a timeless catalog (6 rows by default):
/// 1st/2nd/3rd place, popular vote, best critic, guesser. Each type
/// is semantically immutable: it can be renamed but not deleted
/// (only deactivated via PATCH IsActive=false), because
/// historical UserAward records reference the type via FK.
///
/// Contest series (`ContestSeries`) — every new contest is a new
/// record (sequential Number within the type, Year, TopicUrl).
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Award Catalog")]
[RequireRole(UserRole.SeniorModerator)]
public class AwardCatalogController : ControllerBase
{
    private readonly IAwardCatalogApiService _awardCatalogApiService;

    /// <inheritdoc />
    public AwardCatalogController(IAwardCatalogApiService awardCatalogApiService)
    {
        _awardCatalogApiService = awardCatalogApiService;
    }

    // ---- AwardType ----

    /// <summary>Create a new award type in the catalog.</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Unknown icon or invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="409">Code is already taken.</response>
    [HttpPost("award-types", Name = nameof(CreateAwardType))]
    [ProducesResponseType(typeof(Envelope<AwardType>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAwardType([FromBody] CreateAwardTypeRequest request) =>
        StatusCode(StatusCodes.Status201Created, await _awardCatalogApiService.CreateType(request));

    /// <summary>Partial award type update.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="400">Unknown icon or invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Type not found.</response>
    [HttpPatch("award-types/{id:guid}", Name = nameof(UpdateAwardType))]
    [ProducesResponseType(typeof(Envelope<AwardType>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAwardType(Guid id, [FromBody] UpdateAwardTypeRequest request) =>
        Ok(await _awardCatalogApiService.UpdateType(id, request));

    /// <summary>Deactivate an award type (IsActive=false). Already granted awards are kept.</summary>
    /// <response code="204">Deactivated.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Type not found.</response>
    [HttpDelete("award-types/{id:guid}", Name = nameof(DeactivateAwardType))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAwardType(Guid id)
    {
        await _awardCatalogApiService.DeactivateType(id);
        return NoContent();
    }

    // ---- ContestSeries ----

    /// <summary>Everyone awarded within a contest series.</summary>
    /// <remarks>
    /// The moderator view of a finished contest: who took which place, with a
    /// link to the work. Ordered as a podium (award type order), not by grant
    /// time.
    /// </remarks>
    /// <param name="id">Series identifier.</param>
    /// <response code="200">The list, possibly empty.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Series not found.</response>
    [HttpGet("contest-series/{id:guid}/awards", Name = nameof(GetContestSeriesAwards))]
    [ProducesResponseType(typeof(ListEnvelope<ContestSeriesAward>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContestSeriesAwards(Guid id) =>
        Ok(await _awardCatalogApiService.GetSeriesAwards(id));

    /// <summary>Create a new contest series.</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="409">A series with this (ContestType, Number) already exists.</response>
    [HttpPost("contest-series", Name = nameof(CreateContestSeries))]
    [ProducesResponseType(typeof(Envelope<ContestSeries>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateContestSeries([FromBody] CreateContestSeriesRequest request) =>
        StatusCode(StatusCodes.Status201Created, await _awardCatalogApiService.CreateSeries(request));

    /// <summary>Partial contest series update.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Series not found.</response>
    [HttpPatch("contest-series/{id:guid}", Name = nameof(UpdateContestSeries))]
    [ProducesResponseType(typeof(Envelope<ContestSeries>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContestSeries(Guid id, [FromBody] UpdateContestSeriesRequest request) =>
        Ok(await _awardCatalogApiService.UpdateSeries(id, request));

    /// <summary>Deactivate a series (IsActive=false). Already granted awards are kept.</summary>
    /// <response code="204">Deactivated.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Series not found.</response>
    [HttpDelete("contest-series/{id:guid}", Name = nameof(DeactivateContestSeries))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateContestSeries(Guid id)
    {
        await _awardCatalogApiService.DeactivateSeries(id);
        return NoContent();
    }
}
