using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Awards;

/// <summary>
/// Public read of the award catalog, contest series and user awards.
/// </summary>
/// <remarks>
/// Catalog editing (types + series) lives in <c>v1/moderation/award-types</c>
/// and <c>v1/moderation/contest-series</c>. Granting/revoking an award is in
/// <c>v1/moderation/users/{username}/awards</c>.
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Awards")]
public class AwardController : ControllerBase
{
    private readonly IAwardApiService _awardApiService;

    /// <inheritdoc />
    public AwardController(IAwardApiService awardApiService)
    {
        _awardApiService = awardApiService;
    }

    /// <summary>Active award types (the catalog).</summary>
    /// <response code="200">List of catalog records.</response>
    [HttpGet("award-types", Name = nameof(GetAwardTypes))]
    [ProducesResponseType(typeof(ListEnvelope<AwardType>), StatusCodes.Status200OK)]
    // A catalogue: the service is called without an identity, so every caller
    // gets the same bytes. Same policy as /v1/games/tags.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetAwardTypes() =>
        Ok(await _awardApiService.GetTypes());

    /// <summary>Active contest series (for the grant UI and results highlighting).</summary>
    /// <response code="200">List of series, newest first (year DESC → season).</response>
    [HttpGet("contest-series", Name = nameof(GetContestSeries))]
    [ProducesResponseType(typeof(ListEnvelope<ContestSeries>), StatusCodes.Status200OK)]
    // A catalogue: the service is called without an identity, so every caller
    // gets the same bytes. Same policy as /v1/games/tags.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetContestSeries() =>
        Ok(await _awardApiService.GetSeries());

    /// <summary>A user's awards.</summary>
    /// <param name="username">Username.</param>
    /// <response code="200">List of awards (newest series first, ordered by type SortOrder within).</response>
    /// <response code="404">User not found.</response>
    [HttpGet("users/{username}/awards", Name = nameof(GetUserAwards))]
    [ProducesResponseType(typeof(ListEnvelope<UserAward>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAwards(string username) =>
        Ok(await _awardApiService.GetUserAwards(username));
}
