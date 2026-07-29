using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IAwardService = DM.Domain.Community.Features.Awards.IAwardService;
using DomainCreateAwardType = DM.Domain.Community.Features.Awards.CreateAwardType;
using DomainUpdateAwardType = DM.Domain.Community.Features.Awards.UpdateAwardType;
using DomainCreateContestSeries = DM.Domain.Community.Features.Awards.CreateContestSeries;
using DomainUpdateContestSeries = DM.Domain.Community.Features.Awards.UpdateContestSeries;

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
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AwardCatalogController(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
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
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAwardType([FromBody] CreateAwardTypeRequest request)
    {
        var domain = _mapper.Map<DomainCreateAwardType>(request);
        var created = await _awardService.CreateTypeAsync(domain);
        var api = _mapper.Map<AwardType>(created);
        return StatusCode(StatusCodes.Status201Created, new Envelope<AwardType>(api));
    }

    /// <summary>Partial award type update.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="400">Unknown icon or invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Type not found.</response>
    [HttpPatch("award-types/{id:guid}", Name = nameof(UpdateAwardType))]
    [ProducesResponseType(typeof(Envelope<AwardType>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAwardType(Guid id, [FromBody] UpdateAwardTypeRequest request)
    {
        var domain = _mapper.Map<DomainUpdateAwardType>(request);
        domain.Id = id;
        var updated = await _awardService.UpdateTypeAsync(domain);
        return Ok(new Envelope<AwardType>(_mapper.Map<AwardType>(updated)));
    }

    /// <summary>Deactivate an award type (IsActive=false). Already granted awards are kept.</summary>
    /// <response code="204">Deactivated.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Type not found.</response>
    [HttpDelete("award-types/{id:guid}", Name = nameof(DeactivateAwardType))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAwardType(Guid id)
    {
        await _awardService.DeactivateTypeAsync(id);
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
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContestSeriesAwards(Guid id)
    {
        var awards = await _awardService.GetSeriesAwardsAsync(id);
        return Ok(new ListEnvelope<ContestSeriesAward>(
            _mapper.Map<IEnumerable<ContestSeriesAward>>(awards)));
    }

    /// <summary>Create a new contest series.</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="409">A series with this (ContestType, Number) already exists.</response>
    [HttpPost("contest-series", Name = nameof(CreateContestSeries))]
    [ProducesResponseType(typeof(Envelope<ContestSeries>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateContestSeries([FromBody] CreateContestSeriesRequest request)
    {
        var domain = _mapper.Map<DomainCreateContestSeries>(request);
        var created = await _awardService.CreateSeriesAsync(domain);
        var api = _mapper.Map<ContestSeries>(created);
        return StatusCode(StatusCodes.Status201Created, new Envelope<ContestSeries>(api));
    }

    /// <summary>Partial contest series update.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Series not found.</response>
    [HttpPatch("contest-series/{id:guid}", Name = nameof(UpdateContestSeries))]
    [ProducesResponseType(typeof(Envelope<ContestSeries>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContestSeries(Guid id, [FromBody] UpdateContestSeriesRequest request)
    {
        var domain = _mapper.Map<DomainUpdateContestSeries>(request);
        domain.Id = id;
        var updated = await _awardService.UpdateSeriesAsync(domain);
        return Ok(new Envelope<ContestSeries>(_mapper.Map<ContestSeries>(updated)));
    }

    /// <summary>Deactivate a series (IsActive=false). Already granted awards are kept.</summary>
    /// <response code="204">Deactivated.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Series not found.</response>
    [HttpDelete("contest-series/{id:guid}", Name = nameof(DeactivateContestSeries))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateContestSeries(Guid id)
    {
        await _awardService.DeactivateSeriesAsync(id);
        return NoContent();
    }
}
