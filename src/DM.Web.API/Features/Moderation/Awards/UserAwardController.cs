using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IAwardService = DM.Domain.Community.Features.Awards.IAwardService;

namespace DM.Web.API.Features.Moderation.Awards;

/// <summary>
/// Granting and revoking user awards.
/// </summary>
/// <remarks>
/// A grant creates a `UserAward` referencing `AwardType` (the type) and optionally
/// `ContestSeries` (the contest series). Revocation is a soft-delete: the record is marked
/// IsRemoved=true with the moderator and time recorded; it is not physically deleted
/// (audit). All endpoints require SeniorModerator.
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("User Awards")]
[RequireRole(UserRole.SeniorModerator)]
public class UserAwardController : ControllerBase
{
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserAwardController(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    /// <summary>Grant an award to a user.</summary>
    /// <param name="username">Recipient's username.</param>
    /// <param name="request">awardTypeId + optional contestSeriesId.</param>
    /// <response code="201">Award granted.</response>
    /// <response code="400">Type/series inactive or invalid data.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">User, type or series not found.</response>
    [HttpPost("users/{username}/awards", Name = nameof(GrantUserAward))]
    [ProducesResponseType(typeof(Envelope<UserAward>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantUserAward(string username, [FromBody] GrantUserAwardRequest request)
    {
        var granted = await _awardService.GrantAsync(username, request.AwardTypeId, request.ContestSeriesId, request.WorkUrl);
        var api = _mapper.Map<UserAward>(granted);
        return CreatedAtRoute(
            nameof(AwardController.GetUserAwards),
            new { username },
            new Envelope<UserAward>(api));
    }

    /// <summary>Revoke a previously granted award (soft-delete).</summary>
    /// <param name="username">Username (for URL consistency).</param>
    /// <param name="awardId">Grant record identifier.</param>
    /// <response code="204">Revoked.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    /// <response code="404">Record not found.</response>
    [HttpDelete("users/{username}/awards/{awardId:guid}", Name = nameof(RevokeUserAward))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeUserAward(string username, Guid awardId)
    {
        _ = username; // route param for URL consistency, validation is by awardId
        await _awardService.RevokeAsync(awardId);
        return NoContent();
    }
}
