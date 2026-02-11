using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using DM.Web.Core.Authentication.Credentials;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// Authentication controller for user login/logout operations
/// </summary>
/// <remarks>
/// Manages user sessions using cookie-based authentication (BFF pattern).
/// All endpoints are rate-limited to prevent brute-force attacks.
/// Session cookies are HttpOnly and SameSite=Strict for security.
/// </remarks>
[ApiController]
[Route("v1/account/login")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Login")]
[EnableRateLimiting("auth")]
public class LoginController : ControllerBase
{
    private readonly ILoginApiService _loginApiService;

    /// <summary>
    /// Creates a new instance of LoginController
    /// </summary>
    public LoginController(
        ILoginApiService loginApiService)
    {
        _loginApiService = loginApiService;
    }

    /// <summary>
    /// Authenticate user and create session
    /// </summary>
    /// <remarks>
    /// On successful authentication:
    /// - Creates a session with 1 year expiration
    /// - Sets HttpOnly session cookie
    /// - Returns user profile with current settings
    ///
    /// The identifier can be either:
    /// - Username (login)
    /// - Email address
    ///
    /// On failure:
    /// - 400 with specific field errors (wrong identifier or wrong password)
    /// - 403 if account is banned, inactive, or removed
    /// </remarks>
    /// <param name="credentials">Login credentials (identifier can be email or username)</param>
    /// <response code="200">Authentication successful, returns user profile</response>
    /// <response code="400">Invalid credentials - check error details for specific field</response>
    /// <response code="403">Account is banned, inactive, or removed</response>
    /// <response code="429">Too many login attempts, try again later</response>
    [HttpPost(Name = nameof(Login))]
    [ProducesResponseType(typeof(Envelope<User>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 429)]
    public async Task<IActionResult> Login([FromBody] LoginCredentials credentials)
    {
        // Honeypot validation - reject if the Website field is filled
        if (!string.IsNullOrWhiteSpace(credentials.Website))
        {
            return BadRequest(new BadRequestError(
                "Invalid request",
                new Dictionary<string, IEnumerable<string>>
                {
                    ["identifier"] = new[] { "Invalid login attempt" }
                }));
        }

        return Ok(await _loginApiService.Login(credentials, HttpContext));
    }

    /// <summary>
    /// Logout from current session
    /// </summary>
    /// <remarks>
    /// Terminates the current session and clears the session cookie.
    /// Other sessions on other devices remain active.
    /// </remarks>
    /// <response code="204">Session terminated successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="429">Too many requests, try again later</response>
    [HttpDelete(Name = nameof(Logout))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 429)]
    public async Task<IActionResult> Logout()
    {
        await _loginApiService.Logout(HttpContext);
        return NoContent();
    }

    /// <summary>
    /// Logout from all devices
    /// </summary>
    /// <remarks>
    /// Terminates all active sessions for the current user, including the current one.
    /// This is useful when the user suspects their account has been compromised.
    /// After this call, the user will need to log in again on all devices.
    /// </remarks>
    /// <response code="204">All sessions terminated successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="429">Too many requests, try again later</response>
    [HttpDelete("all", Name = nameof(LogoutAll))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 429)]
    public async Task<IActionResult> LogoutAll()
    {
        await _loginApiService.LogoutAll(HttpContext);
        return NoContent();
    }
}