using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Account.Availability;

/// <summary>
/// Availability checks for usernames and emails
/// </summary>
/// <remarks>
/// Used during registration and credential changes to check if identifiers are available.
/// All endpoints are rate-limited to prevent enumeration attacks.
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Availability")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityApiService _availabilityApiService;

    /// <summary>
    /// Creates a new instance of AvailabilityController
    /// </summary>
    public AvailabilityController(IAvailabilityApiService availabilityApiService)
    {
        _availabilityApiService = availabilityApiService;
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
    [EnableRateLimiting("email-check")]
    [ProducesResponseType(typeof(EmailAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CheckEmail([FromQuery] string email)
    {
        var result = await _availabilityApiService.CheckEmailAvailability(email);
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
    [EnableRateLimiting("username-check")]
    [ProducesResponseType(typeof(UsernameAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CheckUsername([FromQuery] string username)
    {
        var result = await _availabilityApiService.CheckUsernameAvailability(username);
        return Ok(result);
    }
}
