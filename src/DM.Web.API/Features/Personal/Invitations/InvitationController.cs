using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Personal.Invitations;

/// <summary>
/// My invitations (games and blogs)
/// </summary>
/// <remarks>
/// Provides endpoints for managing user's own invitations to games and blogs.
/// For game master operations (sending invitations), see Game/InvitationController.
/// For blog owner operations, see Blog/InvitationController.
/// </remarks>
[ApiController]
[Route("v1/users/me/invitations")]
[ApiExplorerSettings(GroupName = "Personal")]
[Tags("Invitations")]
[AuthenticationRequired]
[EnableRateLimiting("default")]
public class InvitationController : ControllerBase
{
    private readonly IPersonalInvitationApiService _invitationApiService;

    /// <inheritdoc />
    public InvitationController(IPersonalInvitationApiService invitationApiService)
    {
        _invitationApiService = invitationApiService;
    }

    /// <summary>
    /// Get my pending invitations
    /// </summary>
    /// <remarks>
    /// Returns a list of all pending invitations (games and blogs) for the authenticated user.
    /// Invitations can be accepted or rejected using the respective endpoints.
    /// </remarks>
    /// <response code="200">List of invitations</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetMyInvitations))]
    [ProducesResponseType(typeof(ListEnvelope<ReceivedInvitation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyInvitations()
    {
        var invitations = await _invitationApiService.GetMyInvitations();
        return Ok(new ListEnvelope<ReceivedInvitation>(invitations));
    }

    /// <summary>
    /// Accept an invitation
    /// </summary>
    /// <remarks>
    /// Accepts a pending invitation and joins the game/blog as player, reader, or assistant.
    /// Only the invited user can accept their own invitations.
    /// </remarks>
    /// <param name="id">Invitation ID</param>
    /// <response code="204">Invitation accepted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Invitation not found</response>
    /// <response code="410">Invitation expired or already processed</response>
    [HttpPost("{id:guid}/accept", Name = nameof(AcceptInvitation))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status410Gone)]
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
    [HttpPost("{id:guid}/reject", Name = nameof(RejectInvitation))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status410Gone)]
    public async Task<IActionResult> RejectInvitation(Guid id)
    {
        await _invitationApiService.RejectInvitation(id);
        return NoContent();
    }
}
