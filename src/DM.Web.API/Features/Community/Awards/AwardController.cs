using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.Awards;
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
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AwardController(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    /// <summary>Active award types (the catalog).</summary>
    /// <response code="200">List of catalog records.</response>
    [HttpGet("award-types", Name = nameof(GetAwardTypes))]
    [ProducesResponseType(typeof(ListEnvelope<AwardType>), StatusCodes.Status200OK)]
    // A catalogue: the service is called without an identity, so every caller
    // gets the same bytes. Same policy as /v1/games/tags.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetAwardTypes()
    {
        var types = await _awardService.GetTypesAsync();
        var items = types.Select(_mapper.Map<AwardType>);
        return Ok(new ListEnvelope<AwardType>(items, null));
    }

    /// <summary>Active contest series (for the grant UI and results highlighting).</summary>
    /// <response code="200">List of series, newest first (year DESC → season).</response>
    [HttpGet("contest-series", Name = nameof(GetContestSeries))]
    [ProducesResponseType(typeof(ListEnvelope<ContestSeries>), StatusCodes.Status200OK)]
    // A catalogue: the service is called without an identity, so every caller
    // gets the same bytes. Same policy as /v1/games/tags.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetContestSeries()
    {
        var series = await _awardService.GetSeriesAsync();
        var items = series.Select(_mapper.Map<ContestSeries>);
        return Ok(new ListEnvelope<ContestSeries>(items, null));
    }

    /// <summary>A user's awards.</summary>
    /// <param name="username">Username.</param>
    /// <response code="200">List of awards (newest series first, ordered by type SortOrder within).</response>
    /// <response code="404">User not found.</response>
    [HttpGet("users/{username}/awards", Name = nameof(GetUserAwards))]
    [ProducesResponseType(typeof(ListEnvelope<UserAward>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAwards(string username)
    {
        var list = await _awardService.GetUserAwardsAsync(username);
        var items = list.Select(_mapper.Map<UserAward>);
        return Ok(new ListEnvelope<UserAward>(items, null));
    }
}
