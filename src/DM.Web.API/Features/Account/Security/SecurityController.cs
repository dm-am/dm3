using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DM.Domain.Core.Enums;

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
    /// <param name="take">Maximum number of events to return (default: 50, max: 100)</param>
    /// <response code="200">List of security events</response>
    /// <response code="400">Take is outside the 1-100 range</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("logs", Name = nameof(GetSecurityLogs))]
    [ProducesResponseType(typeof(ListEnvelope<SecurityEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSecurityLogs(
        [FromQuery] SecurityLogType? type = null,
        // The page size is bounded by the same [Range] as every other list, and
        // for the same reason on both ends. Clipping only the top left zero to
        // mean "no limit" in the Mongo driver, so ?take=0 answered with the
        // whole security journal while the documentation promised max 100.
        //
        // `take`, not `limit`: limit belongs to a keyset page next to a cursor
        // (API_DESIGN.md), and this list has no cursor — it is a cap.
        [FromQuery][Range(1, 100, ErrorMessage = "Размер страницы должен быть от 1 до 100")] int take = 50)
    {
        var events = await _securityApiService.GetSecurityLogs(type, take);
        return Ok(new ListEnvelope<SecurityEvent>(events));
    }
}
