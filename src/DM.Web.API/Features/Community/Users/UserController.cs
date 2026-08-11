using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// User management API for public user profiles
/// </summary>
/// <remarks>
/// Provides read-only endpoints for viewing public user profiles and searching users.
/// For profile updates use /v1/account endpoints.
/// For moderation use /v1/moderation/users endpoints.
/// </remarks>
[ApiController]
[Route("v1/users")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Users")]
public class UserController : ControllerBase
{
    private readonly ICommunityUserApiService _userApiService;

    /// <inheritdoc />
    public UserController(ICommunityUserApiService userApiService)
    {
        _userApiService = userApiService;
    }

    /// <summary>
    /// Get list of activated users
    /// </summary>
    /// <param name="query">Filtering and pagination parameters</param>
    /// <response code="200">Paginated list of users</response>
    [HttpGet("", Name = nameof(GetUsers))]
    [ProducesResponseType(typeof(ListEnvelope<User>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] UsersQuery query) =>
        Ok(await _userApiService.GetUsers(query));

    /// <summary>
    /// Get user by username
    /// </summary>
    /// <param name="username">User username</param>
    /// <response code="200">User profile retrieved successfully</response>
    /// <response code="404">User not found or was deleted</response>
    [HttpGet("{username}", Name = nameof(GetUserByUsername))]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByUsername(string username) => Ok(await _userApiService.GetUser(username));

    /// <summary>
    /// Get user public profile by username
    /// </summary>
    /// <param name="username">User username</param>
    /// <response code="200">User profile retrieved successfully</response>
    /// <response code="404">User not found or was deleted</response>
    [HttpGet("{username}/profile", Name = nameof(GetUserProfile))]
    [ProducesResponseType(typeof(Envelope<UserProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProfile(string username) => Ok(await _userApiService.GetUserProfile(username));

    /// <summary>
    /// Get login history for a user
    /// </summary>
    /// <remarks>
    /// Requires admin role: login records contain personal data (IP addresses
    /// and user agents). Users can view their own login history via
    /// the /v1/account/security endpoints.
    /// </remarks>
    /// <param name="username">User username</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">Login history retrieved successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to view login history</response>
    /// <response code="404">User not found or was deleted</response>
    [HttpGet("{username}/login-history", Name = nameof(GetLoginHistory))]
    [RequireRole(UserRole.Admin)]
    [ProducesResponseType(typeof(ListEnvelope<LoginHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLoginHistory(string username, [FromQuery] PagingQuery q) =>
        Ok(await _userApiService.GetLoginHistory(username, q));
}
