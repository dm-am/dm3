using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// Account security: password, email, sessions
/// </summary>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Security")]
public class SecurityController : ControllerBase
{
    private readonly IPasswordResetApiService _passwordResetApiService;
    private readonly IEmailChangeApiService _emailChangeApiService;
    private readonly ILoginApiService _loginApiService;

    /// <inheritdoc />
    public SecurityController(
        IPasswordResetApiService passwordResetApiService,
        IEmailChangeApiService emailChangeApiService,
        ILoginApiService loginApiService)
    {
        _passwordResetApiService = passwordResetApiService;
        _emailChangeApiService = emailChangeApiService;
        _loginApiService = loginApiService;
    }

    /// <summary>
    /// Reset registered user password
    /// </summary>
    /// <remarks>
    /// Initiates password reset flow. If the login/email combination is valid,
    /// a password reset email will be sent. For security reasons, always returns
    /// success regardless of whether the account exists.
    /// </remarks>
    /// <param name="resetPassword">Account login and email for verification</param>
    /// <response code="200">Password reset email sent (if account exists)</response>
    /// <response code="400">Invalid request format</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPost("password", Name = nameof(ResetPassword))]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 429)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPassword resetPassword)
    {
        await _passwordResetApiService.Reset(resetPassword);
        return Ok();
    }

    /// <summary>
    /// Change registered user password
    /// </summary>
    /// <remarks>
    /// Changes the password for the authenticated user.
    /// Requires either the old password or a valid password reset token.
    /// </remarks>
    /// <param name="changePassword">Password change request</param>
    /// <response code="200">Password has been changed successfully</response>
    /// <response code="400">Invalid request (wrong old password, invalid token, or weak new password)</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPatch("password", Name = nameof(ChangePassword))]
    [AuthenticationRequired]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(Envelope<User>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 429)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePassword changePassword) =>
        Ok(await _passwordResetApiService.Change(changePassword));

    /// <summary>
    /// Change registered user email
    /// </summary>
    /// <remarks>
    /// Changes the email address for the authenticated user.
    /// Requires the current password for verification.
    /// A confirmation email will be sent to the new address.
    /// </remarks>
    /// <param name="changeEmail">Email change request</param>
    /// <response code="200">Email change request processed, confirmation sent to new address</response>
    /// <response code="400">Invalid request (wrong password, invalid email format, or email already in use)</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPatch("email", Name = nameof(ChangeEmail))]
    [AuthenticationRequired]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(Envelope<User>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 429)]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmail changeEmail) =>
        Ok(await _emailChangeApiService.Change(changeEmail));

    /// <summary>
    /// Confirm email change via token
    /// </summary>
    /// <remarks>
    /// Confirms the email change using the token sent to the new email address.
    /// The token is valid for 24 hours.
    /// </remarks>
    /// <param name="token">Email change confirmation token</param>
    /// <response code="200">Email change confirmed successfully</response>
    /// <response code="410">Token is invalid or expired</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPost("email/{token:guid}", Name = nameof(ConfirmEmailChange))]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    [ProducesResponseType(typeof(GeneralError), 429)]
    public async Task<IActionResult> ConfirmEmailChange(Guid token)
    {
        await _emailChangeApiService.ConfirmEmailChange(token);
        return Ok();
    }

    /// <summary>
    /// Get active sessions for current user
    /// </summary>
    /// <response code="200">List of active sessions</response>
    /// <response code="401">User not authenticated</response>
    [HttpGet("sessions", Name = nameof(GetSessions))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<SessionInfo>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetSessions() =>
        Ok(await _loginApiService.GetSessions());

    /// <summary>
    /// Terminate a specific session
    /// </summary>
    /// <param name="id">Session ID to terminate</param>
    /// <response code="204">Session terminated successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">Session not found</response>
    [HttpDelete("sessions/{id:guid}", Name = nameof(TerminateSession))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> TerminateSession(Guid id)
    {
        await _loginApiService.TerminateSession(id);
        return NoContent();
    }
}
