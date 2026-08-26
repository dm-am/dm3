using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Shared.Http;
using DM.Web.API.Shared.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Account.TwoFactor;

/// <summary>
/// Second factor: setting it up, switching it off, and getting back in without it
/// </summary>
/// <remarks>
/// Switching the factor on takes three calls and the factor is off between the
/// first two: a secret is issued, a code from the device confirms it, and only
/// the confirmation switches anything on. That order is what turns "the app saved
/// nothing" or "the clock is adrift" into a refusal now rather than a locked
/// account days later.
///
/// Neither the secret nor a recovery code ever travels in a path: a path is
/// written into the proxy log verbatim.
///
/// The budget is named on each action rather than on the controller, because the
/// two halves of this surface are counted per different thing. What an owner does
/// to his own factor carries a session and is counted per account
/// (<see cref="RateLimitPolicies.TwoFactor" />); the mailed removal path carries
/// nothing and is counted per address, out of the same strict budget as the rest
/// of the credential surface. Written as one attribute on the controller with
/// overrides below, the reader would have to know which of the two wins.
/// </remarks>
[ApiController]
[Route("v1/account/two-factor")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("TwoFactor")]
[AuthenticationRequired]
public class TwoFactorController : ControllerBase
{
    private readonly ITwoFactorApiService _twoFactorApiService;

    /// <summary>
    /// Creates a new instance of TwoFactorController
    /// </summary>
    public TwoFactorController(ITwoFactorApiService twoFactorApiService)
    {
        _twoFactorApiService = twoFactorApiService;
    }

    /// <summary>
    /// State of the second factor
    /// </summary>
    /// <response code="200">State of the factor</response>
    /// <response code="401">User not authenticated</response>
    [HttpGet(Name = nameof(GetTwoFactorStatus))]
    [EnableRateLimiting(RateLimitPolicies.TwoFactor)]
    [ProducesResponseType(typeof(Envelope<TwoFactorStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    // The caller's own security state. Same rule as the session list: an
    // authenticated read of the account area is stored by nothing.
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetTwoFactorStatus() =>
        Ok(new Envelope<TwoFactorStatusResponse>(await _twoFactorApiService.GetStatus()));

    /// <summary>
    /// Issue a secret to set the factor up with
    /// </summary>
    /// <remarks>
    /// The secret is in this answer and nowhere else, ever. Called again before
    /// the factor is confirmed, it replaces the secret rather than repeating it:
    /// there is one live unconfirmed secret at a time and it is always the last
    /// one issued.
    /// </remarks>
    /// <param name="request">Current password</param>
    /// <response code="200">Secret and otpauth URI</response>
    /// <response code="400">Wrong password</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="409">The factor is already on</response>
    [HttpPost("setup", Name = nameof(SetupTwoFactor))]
    [EnableRateLimiting(RateLimitPolicies.TwoFactor)]
    [ProducesResponseType(typeof(Envelope<TwoFactorSetupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SetupTwoFactor([FromBody] TwoFactorSetupRequest request) =>
        Ok(new Envelope<TwoFactorSetupResponse>(await _twoFactorApiService.Setup(request)));

    /// <summary>
    /// Confirm the secret and switch the factor on
    /// </summary>
    /// <remarks>
    /// Also issues the recovery codes, which are in this answer once and never
    /// again, and ends every other session of the account: switching the factor
    /// on while somebody else's year-long session sits inside is locking a door
    /// with the burglar behind it.
    /// </remarks>
    /// <param name="request">First code from the device</param>
    /// <response code="200">Factor switched on, recovery codes returned</response>
    /// <response code="400">The code did not match</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">No secret was issued</response>
    /// <response code="409">The factor is already on</response>
    /// <response code="410">The setup window has closed</response>
    [HttpPost("confirm", Name = nameof(ConfirmTwoFactor))]
    [EnableRateLimiting(RateLimitPolicies.TwoFactor)]
    [ProducesResponseType(typeof(Envelope<RecoveryCodesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] TwoFactorConfirmRequest request) =>
        Ok(new Envelope<RecoveryCodesResponse>(await _twoFactorApiService.Confirm(request)));

    /// <summary>
    /// Switch the factor off
    /// </summary>
    /// <remarks>
    /// Costs the password and a passed second factor, because switching off is a
    /// change of the same weight as switching on. A recovery code counts as the
    /// second factor: otherwise somebody who lost the device could never switch
    /// the factor off at all.
    /// </remarks>
    /// <param name="request">Current password and a passed second factor</param>
    /// <response code="204">Factor switched off</response>
    /// <response code="400">Wrong password, or the code did not match</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="409">The factor is not on</response>
    [HttpPost("disable", Name = nameof(DisableTwoFactor))]
    [EnableRateLimiting(RateLimitPolicies.TwoFactor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DisableTwoFactor(
        [FromBody] TwoFactorConfirmedActionRequest request)
    {
        await _twoFactorApiService.Disable(request);
        return NoContent();
    }

    /// <summary>
    /// Reissue the recovery codes
    /// </summary>
    /// <remarks>
    /// The previous set stops working whole. A set that is half old and half new
    /// means a code crossed off on paper still opens the account.
    /// </remarks>
    /// <param name="request">Current password and a passed second factor</param>
    /// <response code="200">New codes, returned once</response>
    /// <response code="400">Wrong password, or the code did not match</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="409">The factor is not on</response>
    [HttpPost("recovery-codes", Name = nameof(ReissueRecoveryCodes))]
    [EnableRateLimiting(RateLimitPolicies.TwoFactor)]
    [ProducesResponseType(typeof(Envelope<RecoveryCodesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ReissueRecoveryCodes(
        [FromBody] TwoFactorConfirmedActionRequest request) =>
        Ok(new Envelope<RecoveryCodesResponse>(
            await _twoFactorApiService.ReissueRecoveryCodes(request)));

    /// <summary>
    /// Ask, from the mailbox, for the factor to be taken off
    /// </summary>
    /// <remarks>
    /// Anonymous, because the person who needs it cannot sign in. Answers the
    /// same for every address: which accounts have a factor is not something this
    /// endpoint may disclose.
    ///
    /// A letter does not take the factor off. Following the link from it
    /// schedules the removal, ends every session and starts a waiting period any
    /// successful sign-in with the factor calls off. For the ranks that owe a
    /// factor the path is closed altogether: their factor is taken off by the
    /// second administrator.
    /// </remarks>
    /// <param name="request">Address the account answers at</param>
    /// <response code="204">Request accepted, whatever it named</response>
    /// <response code="400">Invalid email format</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("removal", Name = nameof(RequestTwoFactorRemoval))]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestTwoFactorRemoval(
        [FromBody] TwoFactorRemovalRequest request)
    {
        await _twoFactorApiService.RequestRemoval(request);
        return NoContent();
    }

    /// <summary>
    /// Follow the link from the letter: schedule the removal
    /// </summary>
    /// <param name="token">Token from the mailed link, in the X-Dm-Account-Token header</param>
    /// <response code="204">Removal scheduled, sessions ended, second letter sent</response>
    /// <response code="403">The mailed path is closed for this account's rank</response>
    /// <response code="404">Token missing, malformed, invalid or expired</response>
    /// <response code="409">The factor is not on</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("removal/confirm", Name = nameof(ScheduleTwoFactorRemoval))]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ScheduleTwoFactorRemoval(
        [FromHeader(Name = TokenHeaders.Account)] string? token)
    {
        await _twoFactorApiService.ScheduleRemoval(TokenHeaders.ParseAccountToken(token));
        return NoContent();
    }

    /// <summary>
    /// Follow the second link: call the scheduled removal off
    /// </summary>
    /// <remarks>
    /// The other way to call it off is simply to sign in with the factor, which
    /// is the way the real owner has.
    /// </remarks>
    /// <param name="token">Token from the mailed link, in the X-Dm-Account-Token header</param>
    /// <response code="204">Removal called off</response>
    /// <response code="404">Token missing, malformed or invalid</response>
    /// <response code="410">There is no scheduled removal to call off</response>
    /// <response code="429">Too many requests</response>
    [HttpPost("removal/cancel", Name = nameof(CancelTwoFactorRemoval))]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CancelTwoFactorRemoval(
        [FromHeader(Name = TokenHeaders.Account)] string? token)
    {
        await _twoFactorApiService.CancelRemoval(TokenHeaders.ParseAccountToken(token));
        return NoContent();
    }

    /// <summary>
    /// Take a colleague's factor off, as the second administrator
    /// </summary>
    /// <remarks>
    /// The only way a privileged account gets its factor off, and the reason the
    /// site has two administrators. Hands the caller nothing: no session of the
    /// other account and none of its rights - only a way in by password for its
    /// owner, and the end of every session it had. Written into the journal of
    /// both accounts.
    /// </remarks>
    /// <param name="username">Whose factor to take off</param>
    /// <response code="204">Factor taken off, sessions of that account ended</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Caller is not an administrator, or is the owner</response>
    /// <response code="404">No such user</response>
    /// <response code="409">That account has no factor</response>
    [HttpDelete("users/{username}", Name = nameof(ClearTwoFactorForColleague))]
    [EnableRateLimiting(RateLimitPolicies.TwoFactor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClearTwoFactorForColleague(string username)
    {
        await _twoFactorApiService.ClearForColleague(username);
        return NoContent();
    }
}
