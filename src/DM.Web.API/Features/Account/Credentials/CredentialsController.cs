using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Credential management: password, email, username changes
/// </summary>
/// <remarks>
/// All operations require authentication.
/// Email and username changes require confirmation via token sent to email.
/// Username changes additionally require moderator approval.
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Credentials")]
[AuthenticationRequired]
[EnableRateLimiting("auth")]
public class CredentialsController : ControllerBase
{
    private readonly ICredentialsApiService _credentialsService;

    /// <summary>
    /// Creates a new instance of CredentialsController
    /// </summary>
    public CredentialsController(ICredentialsApiService credentialsService)
    {
        _credentialsService = credentialsService;
    }

    #region Password

    /// <summary>
    /// Change password
    /// </summary>
    /// <remarks>
    /// Changes password for authenticated user.
    /// Requires current password for verification.
    /// </remarks>
    /// <param name="request">Current and new passwords</param>
    /// <response code="200">Password changed successfully</response>
    /// <response code="400">Invalid request (wrong current password or weak new password)</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("password", Name = nameof(ChangePassword))]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePassword([FromBody] PasswordChangeRequest request) =>
        Ok(await _credentialsService.ChangePassword(request));

    #endregion

    #region Email Change

    /// <summary>
    /// Request email change
    /// </summary>
    /// <remarks>
    /// Initiates email change process.
    /// A confirmation link will be sent to the new email address.
    /// The change is not effective until confirmed.
    /// </remarks>
    /// <param name="request">New email and current password</param>
    /// <response code="200">Confirmation email sent to new address</response>
    /// <response code="400">Invalid request (wrong password, invalid email, or email taken)</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("email-change", Name = nameof(RequestEmailChange))]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestEmailChange([FromBody] EmailChangeRequest request) =>
        Ok(await _credentialsService.RequestEmailChange(request));

    /// <summary>
    /// Complete email change
    /// </summary>
    /// <remarks>
    /// Confirms email change using token from confirmation email.
    /// Token is valid for 24 hours.
    /// </remarks>
    /// <param name="token">Confirmation token from email</param>
    /// <response code="204">Email changed successfully</response>
    /// <response code="404">Token invalid or expired</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("email-change/{token:guid}", Name = nameof(ConfirmEmailChange))]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ConfirmEmailChange(Guid token)
    {
        await _credentialsService.ConfirmEmailChange(token);
        return NoContent();
    }

    #endregion

    #region Username Change

    /// <summary>
    /// Request permission to change username
    /// </summary>
    /// <remarks>
    /// Creates a username change request for moderator review.
    /// Only one pending request allowed per user.
    ///
    /// Flow:
    /// 1. Submit request with reason
    /// 2. Wait for moderator approval
    /// 3. Receive notification with approval token
    /// 4. Choose new username and complete change
    /// </remarks>
    /// <param name="request">Request with reason for change</param>
    /// <response code="200">Request created, awaiting moderator review</response>
    /// <response code="400">Already have pending request or invalid input</response>
    /// <response code="401">User not authenticated</response>
    [HttpPost("username-change", Name = nameof(RequestUsernameChange))]
    [ProducesResponseType(typeof(UsernameChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RequestUsernameChange([FromBody] UsernameChangeCreateRequest request) =>
        Ok(await _credentialsService.RequestUsernameChangeAsync(request));

    /// <summary>
    /// Get current username change request status
    /// </summary>
    /// <remarks>
    /// Returns the latest username change request for current user.
    /// Use this to check if a request is pending, approved, or rejected.
    /// </remarks>
    /// <response code="200">Request found</response>
    /// <response code="204">No active request</response>
    /// <response code="401">User not authenticated</response>
    [HttpGet("username-change", Name = nameof(GetUsernameChangeStatus))]
    [ProducesResponseType(typeof(UsernameChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsernameChangeStatus()
    {
        var result = await _credentialsService.GetUsernameChangeStatusAsync();
        return result != null ? Ok(result) : NoContent();
    }

    /// <summary>
    /// Get username change approval status by token
    /// </summary>
    /// <remarks>
    /// After moderator approves the request, user receives a token.
    /// Use this endpoint to check if the token is valid and get request details.
    /// </remarks>
    /// <param name="token">Approval token from notification</param>
    /// <response code="200">Token valid, ready to choose username</response>
    /// <response code="404">Token invalid or expired</response>
    [HttpGet("username-change/{token:guid}", Name = nameof(GetUsernameChangeApproval))]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UsernameChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsernameChangeApproval(Guid token)
    {
        var result = await _credentialsService.GetUsernameChangeApprovalAsync(token);
        if (result == null)
            throw new HttpException(HttpStatusCode.NotFound, "Invalid or expired approval token");
        return Ok(result);
    }

    /// <summary>
    /// Complete username change with chosen name
    /// </summary>
    /// <remarks>
    /// After moderator approval, use the token to select your new username.
    /// The username will be validated for availability.
    /// Token expires 48 hours after approval.
    /// </remarks>
    /// <param name="token">Approval token from notification</param>
    /// <param name="request">New username to use</param>
    /// <response code="200">Username changed successfully</response>
    /// <response code="400">Invalid username or username not available</response>
    /// <response code="404">Token invalid or expired</response>
    [HttpPost("username-change/{token:guid}", Name = nameof(CompleteUsernameChange))]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UsernameChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteUsernameChange(Guid token, [FromBody] UsernameChangeCompletionRequest request) =>
        Ok(await _credentialsService.CompleteUsernameChangeAsync(token, request));

    #endregion
}
