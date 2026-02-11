using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Moderation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace DM.Web.API.Controllers.v1.Moderation;

/// <summary>
/// Moderation and administration tools
/// </summary>
/// <remarks>
/// Provides endpoints for user management, role assignment, and moderation actions.
/// Most endpoints require elevated permissions (Moderator role or higher).
///
/// ## Available Actions
/// - View all registered users with their roles
/// - Assign roles to users (Admin only in production)
/// - Ban/unban users (coming soon)
/// - Review reported content (coming soon)
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Moderation")]
public class ModerationController : ControllerBase
{
    private readonly IModerationApiService _moderationApiService;
    private readonly IWebHostEnvironment _environment;

    /// <inheritdoc />
    public ModerationController(
        IModerationApiService moderationApiService,
        IWebHostEnvironment environment)
    {
        _moderationApiService = moderationApiService;
        _environment = environment;
    }

    /// <summary>
    /// Change user role (DEVELOPMENT ONLY)
    /// </summary>
    /// <remarks>
    /// Assigns a new role to the current authenticated user.
    /// **This endpoint is only available in development environment.**
    ///
    /// **Available roles:**
    /// - `Player` (0) - Regular user
    /// - `Mentor` (1) - Can mentor new players
    /// - `Moderator` (2) - Can moderate content
    /// - `SeniorModerator` (3) - Extended moderation rights
    /// - `Admin` (4) - Full administrative access
    /// </remarks>
    /// <param name="role">New role to assign</param>
    /// <response code="204">Role changed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Endpoint not available in production</response>
    [HttpPost("users/me/role/{role}", Name = nameof(SetMyRole))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> SetMyRole(UserRole role)
    {
        // Security: This endpoint is only available in development
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        await _moderationApiService.SetRole(role);
        return NoContent();
    }

    /// <summary>
    /// Get all registered users (DEVELOPMENT ONLY)
    /// </summary>
    /// <remarks>
    /// Returns a list of all non-deleted users with their roles.
    /// Useful for testing authentication with different roles.
    /// **This endpoint is only available in development environment.**
    ///
    /// **Response includes:**
    /// - User login
    /// - Current role
    ///
    /// Users are sorted by role (highest first), then by login alphabetically.
    /// </remarks>
    /// <response code="200">List of all users</response>
    /// <response code="404">Endpoint not available in production</response>
    [HttpGet("users", Name = nameof(GetAllUsers))]
    [ProducesResponseType(typeof(IReadOnlyList<TestAccountInfo>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetAllUsers()
    {
        // Security: This endpoint is only available in development
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var users = await _moderationApiService.GetAllUsers();
        return Ok(users);
    }

    /// <summary>
    /// Moderate user profile
    /// </summary>
    /// <remarks>
    /// Allows SeniorModerator or higher to edit user profile Info field.
    /// Used to remove inappropriate content from user profiles.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <param name="profile">Profile moderation data</param>
    /// <response code="200">Profile moderated successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">SeniorModerator or higher role required</response>
    /// <response code="404">User not found</response>
    [HttpPatch("users/{login}/profile", Name = nameof(ModerateUserProfile))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(Envelope<UserDetails>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> ModerateUserProfile(string login, [FromBody] ModerateProfile profile) =>
        Ok(await _moderationApiService.ModerateUserProfile(login, profile));

    /// <summary>
    /// Seed test users (DEVELOPMENT ONLY)
    /// </summary>
    /// <remarks>
    /// Creates test users for development and testing.
    /// **This endpoint is only available in development environment.**
    ///
    /// Creates users with various roles and states:
    /// - TestAdmin, TestSeniorMod, TestModerator, TestMentor, TestUser
    /// - Edge cases: Ab (min login), LongestLoginPossible (max login)
    /// - Special states: TestHonorary
    /// - Pending registration: inactive@test.local (for testing activation flow)
    ///
    /// All users have password: Test123!
    ///
    /// Existing users with same login are skipped.
    /// </remarks>
    /// <response code="200">Seed completed with results</response>
    /// <response code="404">Endpoint not available in production</response>
    [HttpPost("seed", Name = nameof(SeedTestUsers))]
    [ProducesResponseType(typeof(SeedResult), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> SeedTestUsers()
    {
        // Security: This endpoint is only available in development
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await _moderationApiService.SeedTestUsers();
        return Ok(result);
    }
}
