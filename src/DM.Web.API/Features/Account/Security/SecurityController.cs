using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Http;

namespace DM.Web.API.Features.Account.Security;

/// <summary>
/// Security event log and account security information
/// </summary>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Security")]
[AuthenticationRequired]
public class SecurityController : ControllerBase
{
    private readonly SecurityApiService _securityApiService;

    public SecurityController(SecurityApiService securityApiService)
    {
        _securityApiService = securityApiService;
    }

    /// <summary>
    /// Get security event logs
    /// </summary>
    /// <remarks>
    /// Returns security events for the current user. Events are sorted by date (newest first).
    /// </remarks>
    /// <param name="type">Optional filter by event type (login, password, session)</param>
    /// <param name="take">Maximum number of events to return (default: 50, max: 100)</param>
    /// <response code="200">List of security events</response>
    /// <response code="400">Take is outside the 1-100 range</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("logs", Name = nameof(GetSecurityLogs))]
    [ProducesResponseType(typeof(ListEnvelope<SecurityEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [PrivateResponse]
    public async Task<IActionResult> GetSecurityLogs(
        [FromQuery] SecurityLogType? type = null,
        // `take`, not `limit`: limit goes with a cursor (API_DESIGN.md), this is a cap.
        [FromQuery][PageSize] int take = 50)
    {
        var events = await _securityApiService.GetSecurityLogs(type, take);
        return Ok(new ListEnvelope<SecurityEvent>(events));
    }
}
