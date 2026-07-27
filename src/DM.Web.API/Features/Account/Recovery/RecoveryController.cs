using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.Dto;
using Microsoft.Extensions.Logging;
using DM.Web.API.Features.Community.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Account.Recovery;

/// <summary>
/// Account recovery: password reset and activation resend
/// </summary>
/// <remarks>
/// Provides account recovery options for users who:
/// - Forgot their password (sends password reset link)
/// - Didn't receive activation email (resends activation)
///
/// All operations are rate-limited to prevent abuse.
/// Responses are intentionally vague to prevent user enumeration.
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Recovery")]
[EnableRateLimiting("auth")]
public class RecoveryController : ControllerBase
{
    private readonly IRecoveryApiService _recoveryService;
    private readonly ILogger<RecoveryController> _logger;

    /// <summary>
    /// Creates a new instance of RecoveryController
    /// </summary>
    public RecoveryController(
        IRecoveryApiService recoveryService,
        ILogger<RecoveryController> logger)
    {
        _recoveryService = recoveryService;
        _logger = logger;
    }

    /// <summary>
    /// Request account recovery
    /// </summary>
    /// <remarks>
    /// Initiates recovery process based on email status:
    /// - **Active account**: Sends password reset link
    /// - **Pending activation**: Resends activation email
    /// - **Not found**: Reports that the email is not registered
    ///
    /// The response status indicates what action was taken:
    /// - `PasswordResetSent`: Reset link sent to email
    /// - `ActivationResent`: Activation link sent to email
    /// - `NotFound`: Email not in system
    ///
    /// The three statuses are distinguishable, so the endpoint tells the caller
    /// whether an address is registered. That is deliberate — the form has to be
    /// able to say that an address was mistyped — and it is a recorded exception
    /// to the non-disclosure rule, see docs/conventions/SECURITY.md.
    /// </remarks>
    /// <param name="request">Email address to recover</param>
    /// <response code="200">Recovery request processed</response>
    /// <response code="400">Invalid email format</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("recovery", Name = nameof(RequestRecovery))]
    [ProducesResponseType(typeof(RecoveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestRecovery([FromBody] RecoveryRequest request)
    {
        var result = await _recoveryService.Recover(request);
        _logger.IdentifierDisclosed(HttpContext, "recovery", result.Status.ToString());
        return Ok(result);
    }

    /// <summary>
    /// Get password reset token status
    /// </summary>
    /// <remarks>
    /// Check if a password reset token is valid before showing the form.
    ///
    /// Statuses:
    /// - `ready`: Token valid, can proceed with password reset
    /// - `expired`: Token expired, user should request a new one
    /// </remarks>
    /// <param name="token">Password reset token from email</param>
    /// <response code="200">Token info with status</response>
    /// <response code="404">Token not found</response>
    [HttpGet("password-reset/{token:guid}", Name = nameof(GetPasswordResetStatus))]
    [ProducesResponseType(typeof(PasswordResetTokenInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPasswordResetStatus(Guid token)
    {
        var tokenInfo = await _recoveryService.GetTokenInfo(token);
        if (tokenInfo == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Token not found or already used");
        }
        return Ok(tokenInfo);
    }

    /// <summary>
    /// Complete password reset
    /// </summary>
    /// <remarks>
    /// Resets password using token from email.
    /// Token is valid for 24 hours.
    ///
    /// After successful reset, user can login with new password.
    /// </remarks>
    /// <param name="token">Password reset token from email</param>
    /// <param name="request">New password</param>
    /// <response code="200">Password reset successfully</response>
    /// <response code="400">Weak password (too short, etc.)</response>
    /// <response code="410">Token invalid or expired</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("password-reset/{token:guid}", Name = nameof(CompletePasswordReset))]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CompletePasswordReset(Guid token, [FromBody] PasswordResetCompletion request) =>
        Ok(await _recoveryService.ResetPassword(token, request));
}
