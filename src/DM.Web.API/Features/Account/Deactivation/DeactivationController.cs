using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Account.Deactivation;

/// <summary>
/// Account deactivation (soft delete)
/// </summary>
/// <remarks>
/// Allows users to deactivate their account.
/// Deactivation is a soft delete: data is hidden but preserved.
/// Account can potentially be recovered by administrators.
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Deactivation")]
[AuthenticationRequired]
[EnableRateLimiting("auth")]
public class DeactivationController : ControllerBase
{
    private readonly IDeactivationApiService _deactivationService;

    /// <summary>
    /// Creates a new instance of DeactivationController
    /// </summary>
    public DeactivationController(IDeactivationApiService deactivationService)
    {
        _deactivationService = deactivationService;
    }

    /// <summary>
    /// Deactivate account
    /// </summary>
    /// <remarks>
    /// Permanently deactivates the current user's account.
    ///
    /// Effects:
    /// - User cannot log in anymore
    /// - Profile becomes invisible to other users
    /// - Username becomes reserved (cannot be reused)
    /// - User's content remains but author shown as "Deleted User"
    ///
    /// This action cannot be undone by the user.
    /// Only administrators can potentially restore the account.
    /// </remarks>
    /// <param name="request">Confirmation with current password</param>
    /// <response code="204">Account deactivated successfully</response>
    /// <response code="400">Wrong password or invalid request</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("deactivate", Name = nameof(DeactivateAccount))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeactivateAccount([FromBody] DeactivationRequest request)
    {
        await _deactivationService.Deactivate(request);
        return NoContent();
    }
}
