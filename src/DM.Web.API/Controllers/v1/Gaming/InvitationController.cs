using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;
using DM.Web.API.Services.Gaming;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Gaming;

/// <summary>
/// Game invitations API
/// </summary>
[ApiController]
[Route("v1/games/{gameId}/invitations")]
[ApiExplorerSettings(GroupName = "Game")]
public class InvitationController : ControllerBase
{
    private readonly IInvitationApiService _invitationApiService;

    /// <inheritdoc />
    public InvitationController(IInvitationApiService invitationApiService)
    {
        _invitationApiService = invitationApiService;
    }

    /// <summary>
    /// Get all pending invitations for a game
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <response code="200">List of invitations</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User is not game master or assistant</response>
    [HttpGet(Name = nameof(GetGameInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Invitation>), 200)]
    public async Task<IActionResult> GetGameInvitations(Guid gameId)
    {
        var invitations = await _invitationApiService.GetGameInvitations(gameId);
        return Ok(new ListEnvelope<Invitation>(invitations));
    }

    /// <summary>
    /// Invite a player to the game
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="request">Invitation request with user login</param>
    /// <response code="201">Invitation created</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">User not found</response>
    [HttpPost("players", Name = nameof(InvitePlayer))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Invitation>), 201)]
    public async Task<IActionResult> InvitePlayer(Guid gameId, [FromBody] CreateInvitation request)
    {
        var invitation = await _invitationApiService.InvitePlayer(gameId, request.Login);
        return CreatedAtRoute(nameof(GetGameInvitations), new { gameId }, new Envelope<Invitation>(invitation));
    }

    /// <summary>
    /// Invite a reader to the game
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="request">Invitation request with user login</param>
    /// <response code="201">Invitation created</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">User not found</response>
    [HttpPost("readers", Name = nameof(InviteReader))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Invitation>), 201)]
    public async Task<IActionResult> InviteReader(Guid gameId, [FromBody] CreateInvitation request)
    {
        var invitation = await _invitationApiService.InviteReader(gameId, request.Login);
        return CreatedAtRoute(nameof(GetGameInvitations), new { gameId }, new Envelope<Invitation>(invitation));
    }

    /// <summary>
    /// Cancel an invitation
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="tokenId">Invitation token ID</param>
    /// <response code="204">Invitation cancelled</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User is not game master or assistant</response>
    /// <response code="404">Invitation not found</response>
    [HttpDelete("{tokenId}", Name = nameof(CancelGameInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    public async Task<IActionResult> CancelGameInvitation(Guid gameId, Guid tokenId)
    {
        await _invitationApiService.CancelInvitation(tokenId);
        return NoContent();
    }
}
