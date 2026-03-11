using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.UsernameChanges;

/// <summary>
/// Username change request moderation
/// </summary>
/// <remarks>
/// Allows senior moderators to review, approve, or reject username change requests from users.
/// Approving a request will change the user's username and create a username history entry.
/// </remarks>
[ApiController]
[Route("v1/moderation/username-changes")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Username Changes")]
[RequireRole(UserRole.SeniorModerator)]
public class UsernameChangeRequestController : ControllerBase
{
    private readonly IUsernameChangeApiService _usernameChangeApiService;

    /// <inheritdoc />
    public UsernameChangeRequestController(IUsernameChangeApiService usernameChangeApiService)
    {
        _usernameChangeApiService = usernameChangeApiService;
    }

    /// <summary>
    /// Get all pending username change requests
    /// </summary>
    /// <remarks>
    /// Returns all username change requests with Pending status.
    /// Only accessible to senior moderators.
    /// </remarks>
    /// <response code="200">List of pending requests</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Senior moderator role required</response>
    [HttpGet(Name = nameof(GetPendingUsernameChangeRequests))]
    [ProducesResponseType(typeof(ListEnvelope<UsernameChangeRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingUsernameChangeRequests()
    {
        var requests = await _usernameChangeApiService.GetPendingRequestsAsync();
        return Ok(new ListEnvelope<UsernameChangeRequest>(requests));
    }

    /// <summary>
    /// Get username change request by ID
    /// </summary>
    /// <remarks>
    /// Returns detailed information about a specific username change request.
    /// Only accessible to senior moderators.
    /// </remarks>
    /// <param name="id">Request ID</param>
    /// <response code="200">Request details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Senior moderator role required</response>
    /// <response code="404">Request not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetUsernameChangeRequest))]
    [ProducesResponseType(typeof(Envelope<UsernameChangeRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsernameChangeRequest(Guid id)
    {
        var request = await _usernameChangeApiService.GetByIdAsync(id);
        return Ok(new Envelope<UsernameChangeRequest>(request));
    }

    /// <summary>
    /// Resolve (approve/reject) a username change request
    /// </summary>
    /// <remarks>
    /// Approves or rejects a pending username change request.
    /// If approved, the user's username will be immediately changed and a history entry created.
    /// If rejected, the request is closed with the moderator's comment.
    /// Only accessible to senior moderators.
    /// </remarks>
    /// <param name="id">Request ID</param>
    /// <param name="resolve">Resolution details (status and optional comment)</param>
    /// <response code="200">Request resolved successfully</response>
    /// <response code="400">Invalid request (e.g., already resolved, invalid status)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Senior moderator role required</response>
    /// <response code="404">Request not found</response>
    [HttpPatch("{id:guid}", Name = nameof(ResolveUsernameChangeRequest))]
    [ProducesResponseType(typeof(Envelope<UsernameChangeRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveUsernameChangeRequest(Guid id, [FromBody] ResolveUsernameChangeRequest resolve)
    {
        var request = await _usernameChangeApiService.ResolveAsync(id, resolve);
        return Ok(new Envelope<UsernameChangeRequest>(request));
    }
}
