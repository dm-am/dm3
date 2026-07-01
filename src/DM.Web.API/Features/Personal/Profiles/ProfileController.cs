using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Personal.Profiles;

/// <summary>
/// My profile management
/// </summary>
/// <remarks>
/// Provides endpoints for viewing and updating your own profile.
/// Returns PersonalProfile with private fields (email, visibility settings).
/// For public profiles of other users, use GET /v1/users/{username}/profile.
/// </remarks>
[ApiController]
[Route("v1/users/me/profile")]
[ApiExplorerSettings(GroupName = "Personal")]
[Tags("Profiles")]
[AuthenticationRequired]
[EnableRateLimiting("default")]
public class ProfileController : ControllerBase
{
    private readonly IPersonalProfileApiService _profileApiService;

    /// <inheritdoc />
    public ProfileController(IPersonalProfileApiService profileApiService)
    {
        _profileApiService = profileApiService;
    }

    /// <summary>
    /// Get my full profile
    /// </summary>
    /// <remarks>
    /// Returns PersonalProfile including private fields (email, visibility settings).
    /// For public-only view of any user, use GET /v1/users/{username}/profile.
    /// </remarks>
    /// <response code="200">Profile retrieved successfully</response>
    /// <response code="401">Authentication required</response>
    [HttpGet(Name = nameof(GetMyProfile))]
    [ProducesResponseType(typeof(PersonalProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile() =>
        Ok(await _profileApiService.GetMyProfile());

    /// <summary>
    /// Update my profile
    /// </summary>
    /// <remarks>
    /// Updates profile fields. All fields are optional - PATCH updates only provided fields.
    ///
    /// - **status**: User-defined status message
    /// - **name**: Real name
    /// - **location**: User location
    /// - **gender**: Male, Female, Unknown
    /// - **birthday**: { day, month, year? } or null to remove
    /// - **info**: Extended info in BB-code
    /// - **contacts**: Array of { type, value } - replaces all contacts
    /// - **avatarUploadId**: Upload ID from /v1/uploads, null to remove avatar
    /// - **visibility**: { showBirthday, showRating }
    ///
    /// To change username or email, use /v1/account/... endpoints.
    /// </remarks>
    /// <param name="profile">Profile fields to update</param>
    /// <response code="200">Profile updated successfully</response>
    /// <response code="400">Invalid profile data</response>
    /// <response code="401">Authentication required</response>
    [HttpPatch(Name = nameof(UpdateMyProfile))]
    [ProducesResponseType(typeof(PersonalProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfile profile) =>
        Ok(await _profileApiService.UpdateMyProfile(profile));

    /// <summary>
    /// Сбросить мой аватар
    /// </summary>
    /// <remarks>
    /// Снимает связь User → Upload, GC удалит S3-объекты через grace-period.
    /// Идемпотент: вызов на пользователе без аватара возвращает 204.
    /// </remarks>
    /// <response code="204">Аватар сброшен (или его и не было).</response>
    /// <response code="401">Требуется аутентификация.</response>
    [HttpDelete("avatar", Name = nameof(RemoveMyAvatar))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveMyAvatar()
    {
        await _profileApiService.RemoveMyAvatar();
        return NoContent();
    }
}
