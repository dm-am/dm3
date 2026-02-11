using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// Current user profile and settings
/// </summary>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Profile")]
[AuthenticationRequired]
public class ProfileController : ControllerBase
{
    private readonly ILoginApiService _loginApiService;
    private readonly IUserApiService _userApiService;

    /// <inheritdoc />
    public ProfileController(
        ILoginApiService loginApiService,
        IUserApiService userApiService)
    {
        _loginApiService = loginApiService;
        _userApiService = userApiService;
    }

    /// <summary>
    /// Get current user details
    /// </summary>
    /// <remarks>
    /// Returns detailed information about the currently authenticated user,
    /// including profile data, settings, and account status.
    /// </remarks>
    /// <response code="200">Current user details retrieved successfully</response>
    /// <response code="401">User not authenticated</response>
    [HttpGet(Name = nameof(GetMe))]
    [ProducesResponseType(typeof(Envelope<UserDetails>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetMe() =>
        Ok(await _loginApiService.GetCurrent());

    /// <summary>
    /// Update current user profile
    /// </summary>
    /// <remarks>
    /// Updates the profile of the currently authenticated user.
    /// To update avatar, first upload image via /v1/uploads, then pass the uploadId here.
    /// </remarks>
    /// <param name="profile">Profile fields to update</param>
    /// <response code="200">Profile updated successfully</response>
    /// <response code="400">Invalid profile data</response>
    /// <response code="401">User not authenticated</response>
    [HttpPatch(Name = nameof(UpdateProfile))]
    [ProducesResponseType(typeof(Envelope<UserDetails>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfile profile) =>
        Ok(await _userApiService.UpdateCurrentUserProfile(profile));

    /// <summary>
    /// Get current user settings
    /// </summary>
    /// <response code="200">Current user settings</response>
    /// <response code="401">User not authenticated</response>
    [HttpGet("settings", Name = nameof(GetSettings))]
    [ProducesResponseType(typeof(Envelope<UserSettings>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetSettings()
    {
        var userDetails = await _loginApiService.GetCurrent();
        return Ok(new Envelope<UserSettings>(userDetails.Resource.Settings));
    }

    /// <summary>
    /// Update current user settings
    /// </summary>
    /// <param name="settings">Updated settings</param>
    /// <response code="200">Settings updated successfully</response>
    /// <response code="400">Invalid settings</response>
    /// <response code="401">User not authenticated</response>
    [HttpPatch("settings", Name = nameof(UpdateSettings))]
    [ProducesResponseType(typeof(Envelope<UserSettings>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> UpdateSettings([FromBody] UserSettings settings) =>
        Ok(await _loginApiService.UpdateSettings(settings));
}
