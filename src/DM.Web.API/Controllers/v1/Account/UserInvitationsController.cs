using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;
using DM.Web.API.Services.Gaming;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// User invitations API
/// </summary>
[ApiController]
[Route("v1/invitations")]
[ApiExplorerSettings(GroupName = "Account")]
public class UserInvitationsController : ControllerBase
{
    private readonly IInvitationApiService _invitationApiService;

    /// <inheritdoc />
    public UserInvitationsController(IInvitationApiService invitationApiService)
    {
        _invitationApiService = invitationApiService;
    }

    /// <summary>
    /// Get all pending invitations for current user
    /// </summary>
    /// <response code="200">List of invitations</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet(Name = nameof(GetUserInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Invitation>), 200)]
    public async Task<IActionResult> GetUserInvitations()
    {
        var invitations = await _invitationApiService.GetUserInvitations();
        return Ok(new ListEnvelope<Invitation>(invitations));
    }

    /// <summary>
    /// Accept an invitation
    /// </summary>
    /// <param name="tokenId">Invitation token ID</param>
    /// <response code="204">Invitation accepted</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="404">Invitation not found</response>
    /// <response code="410">Invitation expired or already processed</response>
    [HttpPut("{tokenId}/accept", Name = nameof(AcceptInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    public async Task<IActionResult> AcceptInvitation(Guid tokenId)
    {
        await _invitationApiService.AcceptInvitation(tokenId);
        return NoContent();
    }

    /// <summary>
    /// Reject an invitation
    /// </summary>
    /// <param name="tokenId">Invitation token ID</param>
    /// <response code="204">Invitation rejected</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="404">Invitation not found</response>
    /// <response code="410">Invitation expired or already processed</response>
    [HttpPut("{tokenId}/reject", Name = nameof(RejectInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RejectInvitation(Guid tokenId)
    {
        await _invitationApiService.RejectInvitation(tokenId);
        return NoContent();
    }
}
