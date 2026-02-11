using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Moderation;

/// <summary>
/// Login change request moderation
/// </summary>
/// <remarks>
/// Allows senior moderators to review, approve, or reject login change requests from users.
/// Approving a request will change the user's login and create a login history entry.
/// </remarks>
[ApiController]
[Route("v1/moderation/login-changes")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Login Changes")]
[RequireRole(UserRole.SeniorModerator)]
public class LoginChangeRequestController : ControllerBase
{
    private readonly ILoginChangeApiService _loginChangeApiService;

    /// <inheritdoc />
    public LoginChangeRequestController(ILoginChangeApiService loginChangeApiService)
    {
        _loginChangeApiService = loginChangeApiService;
    }

    /// <summary>
    /// Get all pending login change requests
    /// </summary>
    /// <remarks>
    /// Returns all login change requests with Pending status.
    /// Only accessible to senior moderators.
    /// </remarks>
    /// <response code="200">List of pending requests</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Senior moderator role required</response>
    [HttpGet(Name = nameof(GetPendingLoginChangeRequests))]
    [ProducesResponseType(typeof(ListEnvelope<LoginChangeRequestDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> GetPendingLoginChangeRequests() =>
        Ok(await _loginChangeApiService.GetPendingRequests());

    /// <summary>
    /// Get login change request by ID
    /// </summary>
    /// <remarks>
    /// Returns detailed information about a specific login change request.
    /// Only accessible to senior moderators.
    /// </remarks>
    /// <param name="id">Request ID</param>
    /// <response code="200">Request details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Senior moderator role required</response>
    /// <response code="404">Request not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetLoginChangeRequest))]
    [ProducesResponseType(typeof(Envelope<LoginChangeRequestDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetLoginChangeRequest(Guid id) =>
        Ok(await _loginChangeApiService.GetById(id));

    /// <summary>
    /// Resolve (approve/reject) a login change request
    /// </summary>
    /// <remarks>
    /// Approves or rejects a pending login change request.
    /// If approved, the user's login will be immediately changed and a history entry created.
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
    [HttpPatch("{id:guid}", Name = nameof(ResolveLoginChangeRequest))]
    [ProducesResponseType(typeof(Envelope<LoginChangeRequestDto>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> ResolveLoginChangeRequest(Guid id, [FromBody] ResolveLoginChangeRequestDto resolve) =>
        Ok(await _loginChangeApiService.Resolve(id, resolve));
}
