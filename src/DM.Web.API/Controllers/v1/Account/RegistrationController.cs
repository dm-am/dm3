using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// User registration and activation (email-first flow)
/// </summary>
/// <remarks>
/// Registration flow:
/// 1. POST /v1/account - Submit email + password (creates pending registration)
/// 2. User receives activation email
/// 3. GET /v1/account/activate/{token} - Check token status and get email for UI
/// 4. POST /v1/account/activate - Complete activation with chosen Login
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Registration")]
[EnableRateLimiting("auth")]
public class RegistrationController : ControllerBase
{
    private readonly IRegistrationApiService _registrationApiService;
    private readonly IActivationApiService _activationApiService;
    private readonly IRecoveryApiService _recoveryApiService;

    /// <inheritdoc />
    public RegistrationController(
        IRegistrationApiService registrationApiService,
        IActivationApiService activationApiService,
        IRecoveryApiService recoveryApiService)
    {
        _registrationApiService = registrationApiService;
        _activationApiService = activationApiService;
        _recoveryApiService = recoveryApiService;
    }

    /// <summary>
    /// Register new user (Step 1)
    /// </summary>
    /// <remarks>
    /// Creates a pending registration. An activation email will be sent to verify the address.
    /// User will choose their Login (username) after clicking the activation link (Step 2).
    ///
    /// If the email already has a pending registration, it will be replaced with new credentials.
    /// </remarks>
    /// <param name="registration">Email and password for registration</param>
    /// <response code="201">Pending registration created. Check email for activation link.</response>
    /// <response code="400">Validation error (invalid email format, password too short, etc.)</response>
    /// <response code="409">Email already registered (active user exists)</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPost(Name = nameof(Register))]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] Registration registration)
    {
        // Honeypot check for bot protection
        if (!string.IsNullOrWhiteSpace(registration.Website))
        {
            return BadRequest(new BadRequestError(
                "Invalid request",
                new Dictionary<string, IEnumerable<string>>
                {
                    ["email"] = new[] { "Invalid registration attempt" }
                }));
        }

        await _registrationApiService.Register(registration);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Check activation token status
    /// </summary>
    /// <remarks>
    /// Returns token status and email for UI display.
    /// Use this before showing the Login selection form.
    ///
    /// Possible statuses:
    /// - "ready": Token is valid, show Login selection form
    /// - "expired": Token expired (>48h), offer to resend activation email
    /// </remarks>
    /// <param name="token">Activation token from email link</param>
    /// <response code="200">Token info (status and email)</response>
    /// <response code="404">Token not found (already used or invalid)</response>
    [HttpGet("activate/{token:guid}", Name = nameof(GetActivationInfo))]
    [ProducesResponseType(typeof(PendingInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActivationInfo(Guid token)
    {
        var info = await _activationApiService.GetPendingInfo(token);
        if (info == null)
        {
            return NotFound(new GeneralError("Token not found or already used"));
        }

        return Ok(info);
    }

    /// <summary>
    /// Complete activation with Login selection (Step 2)
    /// </summary>
    /// <remarks>
    /// Creates user account with chosen Login (username).
    /// Auto-login via HttpOnly cookie on success.
    ///
    /// Login requirements:
    /// - 2-20 characters
    /// - Letters (any language), numbers, underscores, hyphens only
    /// - Must be unique (not used by active users or in login history)
    ///
    /// The activation is idempotent: if ExpectedEmail matches an existing user
    /// with the same Login, returns success without error.
    /// </remarks>
    /// <param name="request">Token and chosen Login</param>
    /// <response code="200">User created and authenticated</response>
    /// <response code="400">Login validation failed (invalid format, already taken, etc.)</response>
    /// <response code="410">Token expired or not found</response>
    [HttpPost("activate", Name = nameof(Activate))]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Activate([FromBody] ActivationRequest request) =>
        Ok(await _activationApiService.Activate(request, HttpContext));

    /// <summary>
    /// Check Login availability
    /// </summary>
    /// <remarks>
    /// Use this during Login selection to provide real-time feedback.
    /// Rate limited: 20 requests per minute.
    ///
    /// Reasons for unavailability:
    /// - "taken": Login is used by an active user
    /// - "reserved": Login was used by a former user (in login history)
    /// - "invalid_format": Login doesn't match format requirements
    /// </remarks>
    /// <param name="login">Login to check</param>
    /// <response code="200">Availability status</response>
    [HttpGet("check-login", Name = nameof(CheckLogin))]
    [EnableRateLimiting("login-check")]
    [ProducesResponseType(typeof(LoginAvailabilityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckLogin([FromQuery] string login)
    {
        var result = await _activationApiService.CheckLoginAvailability(login);
        return Ok(result);
    }

    /// <summary>
    /// Resend activation email
    /// </summary>
    /// <remarks>
    /// Resends the activation email if a pending registration exists for the email.
    /// Always returns 200 OK regardless of whether the pending registration exists
    /// (to prevent email enumeration).
    ///
    /// This will generate a new token, invalidating any previous activation links.
    /// </remarks>
    /// <param name="resendActivation">Email address</param>
    /// <response code="200">Request processed (email sent if pending registration exists)</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPost("activation/resend", Name = nameof(ResendActivation))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendActivation([FromBody] ResendActivation resendActivation)
    {
        await _activationApiService.ResendActivation(resendActivation);
        return Ok();
    }

    /// <summary>
    /// Unified account recovery (password reset or activation resend)
    /// </summary>
    /// <remarks>
    /// Smart recovery that detects what action is needed:
    /// - If email belongs to an active user → sends password reset email
    /// - If email has pending registration → resends activation email
    /// - If email not found → returns error with suggestion to register
    ///
    /// This is the recommended way to handle "Can't sign in" scenarios.
    /// </remarks>
    /// <param name="request">Email address for recovery</param>
    /// <response code="200">Recovery action performed or email not found</response>
    /// <response code="400">Invalid email format</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPost("recovery", Name = nameof(Recovery))]
    [ProducesResponseType(typeof(RecoveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Recovery([FromBody] RecoveryRequest request)
    {
        var result = await _recoveryApiService.Recover(request);
        return Ok(result);
    }

    /// <summary>
    /// Check email availability for registration
    /// </summary>
    /// <remarks>
    /// Use this for Identifier First registration flow to check if email can be used.
    /// Rate limited: 20 requests per minute.
    ///
    /// Possible results:
    /// - Available: true - Email can be used for registration
    /// - Available: false, Reason: Taken - Email is used by active user
    /// - Available: false, Reason: PendingActivation - Email has pending registration
    /// </remarks>
    /// <param name="email">Email to check</param>
    /// <response code="200">Availability status</response>
    [HttpGet("check-email", Name = nameof(CheckEmail))]
    [EnableRateLimiting("login-check")]
    [ProducesResponseType(typeof(EmailAvailabilityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckEmail([FromQuery] string email)
    {
        var result = await _recoveryApiService.CheckEmailAvailability(email);
        return Ok(result);
    }
}
