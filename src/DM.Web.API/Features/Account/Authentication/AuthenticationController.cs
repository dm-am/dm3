using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Shared.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Authentication and session management
/// </summary>
/// <remarks>
/// Manages user sessions using cookie-based authentication (BFF pattern).
/// All endpoints are rate-limited to prevent brute-force attacks.
/// Session cookies are HttpOnly and SameSite=Strict for security.
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Authentication")]
[EnableRateLimiting("auth")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationApiService _authenticationApiService;

    /// <summary>
    /// Creates a new instance of AuthenticationController
    /// </summary>
    public AuthenticationController(IAuthenticationApiService authenticationApiService)
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
    /// - Creates a session with 30 days expiration (if "Remember Me" is checked)
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
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Honeypot validation - reject if the Website field is filled
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            throw new HttpBadRequestException(
                new Dictionary<string, string> { ["identifier"] = "Invalid login attempt" },
                "Invalid request");
        }

        var result = await _authenticationApiService.Login(request, HttpContext);
        return Ok(result);
    }

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
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status429TooManyRequests)]
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
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> TerminateOtherSessions()
    {
        await _authenticationApiService.LogoutAll(HttpContext);
        return NoContent();
    }
}
