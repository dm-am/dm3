using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Shared.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;
using DM.Web.API.Shared.Http;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Authentication and session management
/// </summary>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Authentication")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthenticationController : ControllerBase
{
    private readonly AuthenticationApiService _authenticationApiService;

    public AuthenticationController(AuthenticationApiService authenticationApiService)
    {
        _authenticationApiService = authenticationApiService;
    }

    /// <summary>
    /// Sign in
    /// </summary>
    /// <remarks>
    /// Authenticates user with email and password.
    ///
    /// On successful authentication:
    /// - Creates a session with 365 days expiration (if "Remember Me" is checked)
    /// - Sets HttpOnly session cookie
    /// - Returns user profile with current settings
    ///
    /// On failure:
    /// - 400 with specific field errors (invalid email or wrong password)
    /// - 403 if account is banned, inactive, or removed
    /// </remarks>
    /// <param name="request">Login credentials (email and password)</param>
    /// <response code="200">Authentication successful, returns user profile</response>
    /// <response code="400">Invalid credentials - check error details for specific field</response>
    /// <response code="403">Account is banned, inactive, or removed</response>
    /// <response code="429">Too many login attempts, try again later</response>
    [HttpPost("login", Name = nameof(Login))]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authenticationApiService.Login(request, HttpContext);
        return Ok(result);
    }

    /// <summary>
    /// Finish signing in with the second factor
    /// </summary>
    /// <remarks>
    /// Called when the first step answered `twoFactorRequired`. The challenge
    /// travels in the short-lived `dm_2fa` cookie the first step issued; this
    /// request carries only the code, which may be a code from the authenticator
    /// app or one of the recovery codes.
    ///
    /// Every way of failing answers the same 400 with the same sentence: a wrong
    /// code, an expired challenge, one nobody issued, a recovery code already
    /// spent and a challenge out of attempts are one refusal. How many attempts
    /// are left is not disclosed, for the reason the password step does not
    /// disclose it either.
    /// </remarks>
    /// <param name="request">Code from the device or a recovery code</param>
    /// <response code="200">Second factor accepted, session created</response>
    /// <response code="400">The second factor was not passed</response>
    /// <response code="403">Account is banned or removed</response>
    /// <response code="429">Too many attempts, try again later</response>
    [HttpPost("login/two-factor", Name = nameof(CompleteTwoFactorLogin))]
    [ProducesResponseType(typeof(Envelope<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CompleteTwoFactorLogin([FromBody] TwoFactorLoginRequest request) =>
        // Enveloped: the bare body of the first step is inherited debt.
        Ok(new Envelope<LoginResponse>(
            await _authenticationApiService.CompleteTwoFactor(request, HttpContext)));

    /// <summary>
    /// Sign out
    /// </summary>
    /// <remarks>
    /// Terminates the current session and clears the session cookie.
    /// Other sessions on other devices remain active.
    /// </remarks>
    /// <response code="204">Session terminated successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="429">Too many requests, try again later</response>
    [HttpDelete("login", Name = nameof(Logout))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout()
    {
        await _authenticationApiService.Logout(HttpContext);
        return NoContent();
    }

    /// <summary>
    /// List active sessions
    /// </summary>
    /// <remarks>
    /// Returns all active sessions for the current user.
    /// Each session includes device info, location, and creation time.
    /// </remarks>
    /// <response code="200">List of active sessions</response>
    /// <response code="401">User not authenticated</response>
    [HttpGet("sessions", Name = nameof(GetSessions))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Session>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [PrivateResponse]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _authenticationApiService.GetSessions();
        return Ok(new ListEnvelope<Session>(sessions));
    }

    /// <summary>
    /// Terminate specific session
    /// </summary>
    /// <remarks>
    /// Terminates a specific session by ID.
    /// The user will be logged out on that device.
    /// </remarks>
    /// <param name="id">Session ID to terminate</param>
    /// <response code="204">Session terminated successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Session not found</response>
    [HttpDelete("sessions/{id:guid}", Name = nameof(TerminateSession))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TerminateSession(Guid id)
    {
        await _authenticationApiService.TerminateSession(id);
        return NoContent();
    }

    /// <summary>
    /// Terminate all sessions except current
    /// </summary>
    /// <remarks>
    /// Terminates all active sessions except the current one.
    /// Useful when the user suspects their account has been compromised.
    /// The user remains logged in on the current device.
    /// </remarks>
    /// <response code="204">All other sessions terminated successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="429">Too many requests, try again later</response>
    [HttpDelete("sessions/others", Name = nameof(TerminateOtherSessions))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> TerminateOtherSessions()
    {
        await _authenticationApiService.LogoutElsewhere(HttpContext);
        return NoContent();
    }
}
