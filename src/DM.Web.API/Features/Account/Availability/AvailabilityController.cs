using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;

namespace DM.Web.API.Features.Account.Availability;

/// <summary>
/// Availability checks for usernames and emails
/// </summary>
/// <remarks>
/// Used during registration and credential changes to check if identifiers are available.
///
/// These endpoints answer whether an identifier is already registered, which is
/// their purpose and which makes them an enumeration surface by design. Rate
/// limiting makes a bulk scan slow, it does not prevent one — see the recorded
/// exception in docs/conventions/SECURITY.md.
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Availability")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityApiService _availabilityApiService;
    private readonly ILogger<AvailabilityController> _logger;

    /// <summary>
    /// Creates a new instance of AvailabilityController
    /// </summary>
    public AvailabilityController(
        IAvailabilityApiService availabilityApiService,
        ILogger<AvailabilityController> logger)
    {
        _availabilityApiService = availabilityApiService;
        _logger = logger;
    }

    /// <summary>
    /// Check email availability
    /// </summary>
    /// <remarks>
    /// Use this during registration to check if an email can be used.
    ///
    /// Possible results:
    /// - IsAvailable: true - Email can be used for registration
    /// - IsAvailable: false, Reason: Taken - Email is used by active user
    /// - IsAvailable: false, Reason: PendingActivation - Email has pending registration
    /// </remarks>
    /// <param name="email">Email to check</param>
    /// <response code="200">Availability status</response>
    /// <response code="429">Too many requests</response>
    [HttpGet("check-email", Name = nameof(CheckEmail))]
    [EnableRateLimiting(RateLimitPolicies.EmailCheck)]
    [ProducesResponseType(typeof(EmailAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CheckEmail([FromQuery] string email)
    {
        var result = await _availabilityApiService.CheckEmailAvailability(email);

        if (result.Reason is EmailUnavailableReason.Taken or EmailUnavailableReason.PendingActivation)
        {
            _logger.IdentifierDisclosed(HttpContext, "check-email", result.Reason.ToString()!);
        }

        return Ok(result);
    }

    /// <summary>
    /// Check username availability
    /// </summary>
    /// <remarks>
    /// Use this during registration or username change to check if a username is available.
    ///
    /// Reasons for unavailability:
    /// - Taken: Username is used by an active user
    /// - Reserved: Username was used by a former user (in username history)
    /// - InvalidFormat: Username doesn't match format requirements
    /// </remarks>
    /// <param name="username">Username to check</param>
    /// <response code="200">Availability status</response>
    /// <response code="429">Too many requests</response>
    [HttpGet("check-username", Name = nameof(CheckUsername))]
    [EnableRateLimiting(RateLimitPolicies.UsernameCheck)]
    [ProducesResponseType(typeof(UsernameAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CheckUsername([FromQuery] string username)
    {
        var result = await _availabilityApiService.CheckUsernameAvailability(username);

        if (result.Reason is UsernameUnavailableReason.Taken or UsernameUnavailableReason.Reserved)
        {
            _logger.IdentifierDisclosed(HttpContext, "check-username", result.Reason.ToString()!);
        }

        return Ok(result);
    }
}
