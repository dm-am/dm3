using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Game.Games;
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
    private readonly IGameApiService _gameApiService;

    /// <inheritdoc />
    public GameInvitationController(
        IGameInvitationApiService invitationApiService,
        IGameApiService gameApiService)
    {
        _invitationApiService = invitationApiService;
        _gameApiService = gameApiService;
    }

    private async Task<Guid> ResolveGameId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : (await _gameApiService.GetByPublicId(id)).Resource.Id;

    /// <summary>
    /// Get all pending invitations for a game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
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
    public async Task<IActionResult> GetGameInvitations(string id)
    {
        var gameId = await ResolveGameId(id);
        var invitations = await _invitationApiService.GetGameInvitations(gameId);
        return Ok(new ListEnvelope<GameInvitation>(invitations));
    }

    /// <summary>
    /// Invite a player to the game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
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
    public async Task<IActionResult> InvitePlayer(string id, [FromBody] CreateInvitationRequest request)
    {
        var gameId = await ResolveGameId(id);
        var invitation = await _invitationApiService.InvitePlayer(gameId, request.Username);
        return CreatedAtRoute(nameof(GetGameInvitations), new { id }, invitation);
    }

    /// <summary>
    /// Invite a reader to the game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
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
    public async Task<IActionResult> InviteReader(string id, [FromBody] CreateInvitationRequest request)
    {
        var gameId = await ResolveGameId(id);
        var invitation = await _invitationApiService.InviteReader(gameId, request.Username);
        return CreatedAtRoute(nameof(GetGameInvitations), new { id }, invitation);
    }

    /// <summary>
    /// Invite an assistant to the game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
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
    public async Task<IActionResult> InviteAssistant(string id, [FromBody] CreateInvitationRequest request)
    {
        var gameId = await ResolveGameId(id);
        var invitation = await _invitationApiService.InviteAssistant(gameId, request.Username);
        return CreatedAtRoute(nameof(GetGameInvitations), new { id }, invitation);
    }

    /// <summary>
    /// Cancel an invitation
    /// </summary>
    /// <remarks>
    /// Cancels a pending invitation. Only the game master or assistant can cancel invitations.
    /// </remarks>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
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
    public async Task<IActionResult> CancelInvitation(string id, Guid invitationId)
    {
        await _invitationApiService.CancelInvitation(invitationId);
        return NoContent();
    }
}
