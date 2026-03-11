using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Invitations;

/// <summary>
/// Game invitations API for masters
/// </summary>
/// <remarks>
/// Provides endpoints for game masters to manage invitations.
/// For user-facing operations (accepting/rejecting), see Personal/InvitationController.
/// </remarks>
[ApiController]
[Route("v1/games/{id}/invitations")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Invitations")]
public class GameInvitationController : ControllerBase
{
    private readonly IGameInvitationApiService _invitationApiService;

    /// <inheritdoc />
    public GameInvitationController(IGameInvitationApiService invitationApiService)
    {
        _invitationApiService = invitationApiService;
    }

    /// <summary>
    /// Get all pending invitations for a game
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <response code="200">List of invitations</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">Game not found</response>
    [HttpGet(Name = nameof(GetGameInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<GameInvitation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameInvitations(Guid id)
    {
        var invitations = await _invitationApiService.GetGameInvitations(id);
        return Ok(new ListEnvelope<GameInvitation>(invitations));
    }

    /// <summary>
    /// Invite a player to the game
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <param name="request">Invitation request with username</param>
    /// <response code="201">Invitation created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">User or game not found</response>
    [HttpPost("players", Name = nameof(InvitePlayer))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(GameInvitation), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InvitePlayer(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var invitation = await _invitationApiService.InvitePlayer(id, request.Username);
        return CreatedAtRoute(nameof(GetGameInvitations), new { id }, invitation);
    }

    /// <summary>
    /// Invite a reader to the game
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <param name="request">Invitation request with username</param>
    /// <response code="201">Invitation created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">User or game not found</response>
    [HttpPost("readers", Name = nameof(InviteReader))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(GameInvitation), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InviteReader(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var invitation = await _invitationApiService.InviteReader(id, request.Username);
        return CreatedAtRoute(nameof(GetGameInvitations), new { id }, invitation);
    }

    /// <summary>
    /// Invite an assistant to the game
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <param name="request">Invitation request with username</param>
    /// <response code="201">Invitation created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master</response>
    /// <response code="404">User or game not found</response>
    [HttpPost("assistants", Name = nameof(InviteAssistant))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(GameInvitation), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InviteAssistant(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var invitation = await _invitationApiService.InviteAssistant(id, request.Username);
        return CreatedAtRoute(nameof(GetGameInvitations), new { id }, invitation);
    }

    /// <summary>
    /// Cancel an invitation
    /// </summary>
    /// <remarks>
    /// Cancels a pending invitation. Only the game master or assistant can cancel invitations.
    /// </remarks>
    /// <param name="id">Game ID</param>
    /// <param name="invitationId">Invitation ID</param>
    /// <response code="204">Invitation cancelled</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">Invitation not found or already processed</response>
    [HttpDelete("{invitationId}", Name = nameof(CancelInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelInvitation(Guid id, Guid invitationId)
    {
        await _invitationApiService.CancelInvitation(invitationId);
        return NoContent();
    }
}
