using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;
using DM.Web.API.Services.Game;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Game;

/// <summary>
/// Game invitations API
/// </summary>
/// <remarks>
/// Provides endpoints for managing game invitations.
///
/// ## Game Master Operations (nested routes)
/// - List pending invitations for a game
/// - Invite players or readers to a game
///
/// ## User Operations (flat routes)
/// - List own pending invitations
/// - Accept or reject invitations
/// - Cancel invitations (master/assistant)
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Invitations")]
public class InvitationController : ControllerBase
{
    private readonly IInvitationApiService _invitationApiService;

    /// <inheritdoc />
    public InvitationController(IInvitationApiService invitationApiService)
    {
        _invitationApiService = invitationApiService;
    }

    // === Game-scoped routes (for masters) ===

    /// <summary>
    /// Get all pending invitations for a game
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <response code="200">List of invitations</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="410">Game not found</response>
    [HttpGet("games/{id}/invitations", Name = nameof(GetGameInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Invitation>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGameInvitations(Guid id)
    {
        var invitations = await _invitationApiService.GetGameInvitations(id);
        return Ok(new ListEnvelope<Invitation>(invitations));
    }

    /// <summary>
    /// Invite a player to the game
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <param name="request">Invitation request with user login</param>
    /// <response code="201">Invitation created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">User not found</response>
    /// <response code="410">Game not found</response>
    [HttpPost("games/{id}/invitations/players", Name = nameof(InvitePlayer))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Invitation>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> InvitePlayer(Guid id, [FromBody] CreateInvitation request)
    {
        var invitation = await _invitationApiService.InvitePlayer(id, request.Login);
        return CreatedAtRoute(nameof(GetGameInvitations), new { id }, new Envelope<Invitation>(invitation));
    }

    /// <summary>
    /// Invite a reader to the game
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <param name="request">Invitation request with user login</param>
    /// <response code="201">Invitation created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">User not found</response>
    /// <response code="410">Game not found</response>
    [HttpPost("games/{id}/invitations/readers", Name = nameof(InviteReader))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Invitation>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> InviteReader(Guid id, [FromBody] CreateInvitation request)
    {
        var invitation = await _invitationApiService.InviteReader(id, request.Login);
        return CreatedAtRoute(nameof(GetGameInvitations), new { id }, new Envelope<Invitation>(invitation));
    }

    // === User-scoped + flat routes ===

    /// <summary>
    /// Get all pending invitations for current user
    /// </summary>
    /// <remarks>
    /// Returns a list of all pending game invitations for the authenticated user.
    /// Invitations can be accepted or rejected using the respective endpoints.
    /// </remarks>
    /// <response code="200">List of invitations</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("invitations", Name = nameof(GetMyInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Invitation>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetMyInvitations()
    {
        var invitations = await _invitationApiService.GetUserInvitations();
        return Ok(new ListEnvelope<Invitation>(invitations));
    }

    /// <summary>
    /// Cancel an invitation
    /// </summary>
    /// <remarks>
    /// Cancels a pending invitation. Only the game master or assistant can cancel invitations.
    /// </remarks>
    /// <param name="id">Invitation ID</param>
    /// <response code="204">Invitation cancelled</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="410">Invitation not found or already processed</response>
    [HttpDelete("invitations/{id}", Name = nameof(CancelInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> CancelInvitation(Guid id)
    {
        await _invitationApiService.CancelInvitation(id);
        return NoContent();
    }

    /// <summary>
    /// Accept an invitation
    /// </summary>
    /// <remarks>
    /// Accepts a pending invitation and joins the game as player or reader.
    /// Only the invited user can accept their own invitations.
    /// </remarks>
    /// <param name="id">Invitation ID</param>
    /// <response code="204">Invitation accepted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Invitation not found</response>
    /// <response code="410">Invitation expired or already processed</response>
    [HttpPost("invitations/{id}/accept", Name = nameof(AcceptInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> AcceptInvitation(Guid id)
    {
        await _invitationApiService.AcceptInvitation(id);
        return NoContent();
    }

    /// <summary>
    /// Reject an invitation
    /// </summary>
    /// <remarks>
    /// Rejects a pending invitation. Only the invited user can reject their own invitations.
    /// </remarks>
    /// <param name="id">Invitation ID</param>
    /// <response code="204">Invitation rejected</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Invitation not found</response>
    /// <response code="410">Invitation expired or already processed</response>
    [HttpPost("invitations/{id}/reject", Name = nameof(RejectInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> RejectInvitation(Guid id)
    {
        await _invitationApiService.RejectInvitation(id);
        return NoContent();
    }
}
