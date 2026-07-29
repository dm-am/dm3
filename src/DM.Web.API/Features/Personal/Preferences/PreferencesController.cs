using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// My preferences management
/// </summary>
/// <remarks>
/// Provides endpoints for viewing and updating user display preferences:
/// - Theme (Light/Dark)
/// - Paging settings
///
/// Preferences control how user sees the website (not part of profile).
/// </remarks>
[ApiController]
[Route("v1/users/me/preferences")]
[ApiExplorerSettings(GroupName = "Personal")]
[Tags("Preferences")]
[AuthenticationRequired]
[EnableRateLimiting(RateLimitPolicies.Default)]
public class PreferencesController : ControllerBase
{
    private readonly IPreferencesApiService _preferencesApiService;

    /// <inheritdoc />
    public PreferencesController(IPreferencesApiService preferencesApiService)
    {
        _preferencesApiService = preferencesApiService;
    }

    /// <summary>
    /// Get my preferences
    /// </summary>
    /// <remarks>
    /// Returns user display preferences (theme, paging).
    /// Preferences are also included in LoginResponse.
    /// </remarks>
    /// <response code="200">Preferences retrieved successfully</response>
    /// <response code="401">Authentication required</response>
    [HttpGet(Name = nameof(GetMyPreferences))]
    [ProducesResponseType(typeof(Preferences), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPreferences() =>
        Ok(await _preferencesApiService.GetMyPreferences());

    /// <summary>
    /// Update my preferences
    /// </summary>
    /// <remarks>
    /// Updates user display preferences. All fields are optional - only provided fields will be updated.
    /// </remarks>
    /// <param name="request">Fields to change</param>
    /// <response code="200">Preferences updated successfully</response>
    /// <response code="400">Invalid preferences data</response>
    /// <response code="401">Authentication required</response>
    [HttpPatch(Name = nameof(UpdateMyPreferences))]
    [ProducesResponseType(typeof(Preferences), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyPreferences([FromBody] UpdatePreferencesRequest request) =>
        Ok(await _preferencesApiService.UpdateMyPreferences(request));
}
