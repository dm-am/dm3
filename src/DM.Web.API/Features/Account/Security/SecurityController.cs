using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Account.Security;

/// <summary>
/// Security event log and account security information
/// </summary>
/// <remarks>
/// Provides access to security-related events for the current user including:
/// - Login attempts (successful and failed)
/// - Password changes
/// - Session management events
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Security")]
[AuthenticationRequired]
public class SecurityController : ControllerBase
{
    private readonly ISecurityApiService _securityApiService;

    /// <inheritdoc />
    public SecurityController(ISecurityApiService securityApiService)
    {
        _securityApiService = securityApiService;
    }

    /// <summary>
    /// Get security event logs
    /// </summary>
    /// <remarks>
    /// Returns security events for the current user. Events are sorted by date (newest first).
    ///
    /// Filter by type:
    /// - `login` - Login attempts (successful and failed)
    /// - `password` - Password changes and reset requests
    /// - `session` - Session creation and termination
    /// - No type specified - All events
    /// </remarks>
    /// <param name="type">Optional filter by event type (login, password, session)</param>
    /// <param name="limit">Maximum number of events to return (default: 50, max: 100)</param>
    /// <response code="200">List of security events</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("logs", Name = nameof(GetSecurityLogs))]
    [ProducesResponseType(typeof(ListEnvelope<SecurityEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSecurityLogs([FromQuery] string? type = null, [FromQuery] int limit = 50)
    {
        var effectiveLimit = limit > 100 ? 100 : limit;
        var events = await _securityApiService.GetSecurityLogs(type, effectiveLimit);
        return Ok(new ListEnvelope<SecurityEvent>(events));
    }
}
