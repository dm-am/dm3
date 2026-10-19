using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
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
/// An enumeration surface by design: the recorded exception in docs/conventions/SECURITY.md.
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Availability")]
public class AvailabilityController : ControllerBase
{
    private readonly AvailabilityApiService _availabilityApiService;
    private readonly ILogger<AvailabilityController> _logger;

    public AvailabilityController(
        AvailabilityApiService availabilityApiService,
        ILogger<AvailabilityController> logger)
    {
        _availabilityApiService = availabilityApiService;
        _logger = logger;
    }

    /// <summary>
    /// Check email availability
    /// </summary>
    /// <remarks>
    /// An address held by a removed account answers Taken, like a live one (SECURITY.md).
    /// </remarks>
    /// <param name="request">Email to check</param>
    /// <response code="200">Availability status</response>
    /// <response code="400">Email missing</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("check-email", Name = nameof(CheckEmail))]
    [EnableRateLimiting(RateLimitPolicies.EmailCheck)]
    [ProducesResponseType(typeof(EmailAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CheckEmail([FromBody] EmailAvailabilityRequest request)
    {
        var result = await _availabilityApiService.CheckEmailAvailability(request.Email);

        if (result.Reason is not null)
        {
            _logger.IdentifierDisclosed(HttpContext, "check-email", result.Reason.ToString()!);
        }

        return Ok(result);
    }

    /// <summary>
    /// Check username availability
    /// </summary>
    /// <param name="request">Username to check</param>
    /// <response code="200">Availability status</response>
    /// <response code="400">Username missing</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("check-username", Name = nameof(CheckUsername))]
    [EnableRateLimiting(RateLimitPolicies.UsernameCheck)]
    [ProducesResponseType(typeof(UsernameAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CheckUsername([FromBody] UsernameAvailabilityRequest request)
    {
        var result = await _availabilityApiService.CheckUsernameAvailability(request.Username);

        if (result.Reason is UsernameUnavailableReason.Taken)
        {
            _logger.IdentifierDisclosed(HttpContext, "check-username", result.Reason.ToString()!);
        }

        return Ok(result);
    }
}
