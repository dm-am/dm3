using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Users;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Endorsements;

/// <summary>
/// User endorsements controller - list, create, update, delete endorsements
/// </summary>
[ApiController]
[Route("v1/users/{username}/endorsements")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("User Endorsements")]
public class UserEndorsementController : ControllerBase
{
    private readonly IUserEndorsementService _endorsementService;
    private readonly IUserLookupService _userLookupService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserEndorsementController(
        IUserEndorsementService endorsementService,
        IUserLookupService userLookupService,
        IMapper mapper)
    {
        _endorsementService = endorsementService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    /// <summary>
    /// Get endorsements for a user
    /// </summary>
    /// <remarks>
    /// Returns positive endorsements written about the specified user.
    /// Users can only endorse other users they have played with in the same game.
    /// Only one endorsement per author-target pair is allowed.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of user endorsements</response>
    /// <response code="404">User not found</response>
    [HttpGet(Name = nameof(GetUserEndorsements))]
    [ProducesResponseType(typeof(ListEnvelope<UserEndorsement>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserEndorsements(string username, [FromQuery] PagingQuery q)
    {
        var user = await _userLookupService.GetAsync(username);
        var (endorsements, paging) = await _endorsementService.GetListAsync(user.UserId, q);
        var apiEndorsements = endorsements.Select(_mapper.Map<UserEndorsement>);
        return Ok(new ListEnvelope<UserEndorsement>(apiEndorsements, new PagingInfo(paging)));
    }

    /// <summary>
    /// Create user endorsement
    /// </summary>
    /// <remarks>
    /// Creates a positive endorsement for the specified user.
    /// You can only endorse users you have played with in the same game.
    /// Only one endorsement per user is allowed.
    /// Requires at least 100 game posts to create endorsements.
    /// </remarks>
    /// <param name="username">Username of user to endorse</param>
    /// <param name="request">Endorsement data</param>
    /// <response code="201">Endorsement created successfully</response>
    /// <response code="400">Invalid endorsement data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (haven't played together, endorsing yourself, or newbie)</response>
    /// <response code="404">User not found</response>
    /// <response code="409">Endorsement already exists</response>
    [HttpPost(Name = nameof(CreateUserEndorsement))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(UserEndorsement), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUserEndorsement(string username, [FromBody] CreateUserEndorsementRequest request)
    {
        var user = await _userLookupService.GetAsync(username);
        var createEndorsement = new CreateUserEndorsement
        {
            TargetUserId = user.UserId,
            Text = request.Text
        };
        var endorsement = await _endorsementService.CreateAsync(createEndorsement);
        var apiEndorsement = _mapper.Map<UserEndorsement>(endorsement);
        return CreatedAtRoute(nameof(GetUserEndorsement), new { id = endorsement.Id }, apiEndorsement);
    }

    /// <summary>
    /// Get single endorsement by ID
    /// </summary>
    /// <param name="id">Endorsement identifier</param>
    /// <response code="200">Endorsement details</response>
    /// <response code="404">Endorsement not found</response>
    [HttpGet("/v1/endorsements/{id}", Name = nameof(GetUserEndorsement))]
    [ProducesResponseType(typeof(UserEndorsement), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserEndorsement(Guid id)
    {
        var endorsement = await _endorsementService.GetAsync(id);
        var apiEndorsement = _mapper.Map<UserEndorsement>(endorsement);
        return Ok(apiEndorsement);
    }

    /// <summary>
    /// Update user endorsement
    /// </summary>
    /// <remarks>
    /// Updates an existing endorsement.
    /// Only the author can edit their endorsement.
    /// Endorsements can only be edited within 24 hours of creation (admins exempt).
    /// </remarks>
    /// <param name="id">Endorsement identifier</param>
    /// <param name="request">Update data</param>
    /// <response code="200">Endorsement updated successfully</response>
    /// <response code="400">Invalid update data</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not author or edit window expired)</response>
    /// <response code="404">Endorsement not found</response>
    [HttpPatch("/v1/endorsements/{id}", Name = nameof(UpdateUserEndorsement))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(UserEndorsement), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserEndorsement(Guid id, [FromBody] UpdateUserEndorsementRequest request)
    {
        var updateEndorsement = new UpdateUserEndorsement
        {
            EndorsementId = id,
            Text = request.Text
        };
        var endorsement = await _endorsementService.UpdateAsync(updateEndorsement);
        var apiEndorsement = _mapper.Map<UserEndorsement>(endorsement);
        return Ok(apiEndorsement);
    }

    /// <summary>
    /// Delete user endorsement
    /// </summary>
    /// <remarks>
    /// Deletes an existing endorsement.
    /// Can be deleted by the author or a moderator.
    /// </remarks>
    /// <param name="id">Endorsement identifier</param>
    /// <response code="204">Endorsement deleted successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed (not author or moderator)</response>
    /// <response code="404">Endorsement not found</response>
    [HttpDelete("/v1/endorsements/{id}", Name = nameof(DeleteUserEndorsement))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUserEndorsement(Guid id)
    {
        await _endorsementService.DeleteAsync(id);
        return NoContent();
    }
}
