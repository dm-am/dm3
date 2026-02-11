using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Community;

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
    private readonly IUserApiService _userApiService;
    private readonly ILoginHistoryApiService _loginHistoryApiService;

    /// <inheritdoc />
    public UserController(
        IUserApiService userApiService,
        ILoginHistoryApiService loginHistoryApiService)
    {
        _userApiService = userApiService;
        _loginHistoryApiService = loginHistoryApiService;
    }

    /// <summary>
    /// Get list of activated users
    /// </summary>
    /// <param name="query">Filtering and pagination parameters</param>
    /// <response code="200">Paginated list of users</response>
    [HttpGet("", Name = nameof(GetUsers))]
    [ProducesResponseType(typeof(ListEnvelope<User>), 200)]
    public async Task<IActionResult> GetUsers([FromQuery] UsersQuery query) =>
        Ok(await _userApiService.GetUsers(query));

    /// <summary>
    /// Get users by role
    /// </summary>
    /// <param name="role">User role to filter by (e.g., Admin, Moderator, Player)</param>
    /// <response code="200">List of users with the specified role</response>
    [HttpGet("by-role/{role}", Name = nameof(GetUsersByRole))]
    [ProducesResponseType(typeof(ListEnvelope<User>), 200)]
    public async Task<IActionResult> GetUsersByRole(UserRole role) =>
        Ok(await _userApiService.GetUsersByRole(role));

    /// <summary>
    /// Get user by login
    /// </summary>
    /// <param name="login">User login (username)</param>
    /// <response code="200">User profile retrieved successfully</response>
    /// <response code="404">User not found or was deleted</response>
    [HttpGet("{login}", Name = nameof(GetUserByLogin))]
    [ProducesResponseType(typeof(Envelope<User>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetUserByLogin(string login) => Ok(await _userApiService.GetUser(login));

    /// <summary>
    /// Get user details by login
    /// </summary>
    /// <param name="login">User login (username)</param>
    /// <response code="200">User details retrieved successfully</response>
    /// <response code="404">User not found or was deleted</response>
    [HttpGet("{login}/details", Name = nameof(GetUserDetails))]
    [ProducesResponseType(typeof(Envelope<UserDetails>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetUserDetails(string login) => Ok(await _userApiService.GetUserDetails(login));

    /// <summary>
    /// Get login history for a user
    /// </summary>
    /// <param name="login">User login (username)</param>
    /// <response code="200">Login history retrieved successfully</response>
    /// <response code="404">User not found or was deleted</response>
    [HttpGet("{login}/login-history", Name = nameof(GetLoginHistory))]
    [ProducesResponseType(typeof(ListEnvelope<LoginHistoryDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetLoginHistory(string login) => Ok(await _loginHistoryApiService.GetByLogin(login));

    /// <summary>
    /// Get best post (highest rated) by user from open rooms
    /// </summary>
    /// <param name="login">User login (username)</param>
    /// <response code="200">Best post retrieved successfully</response>
    /// <response code="404">User not found or no best post found</response>
    [HttpGet("{login}/best-post", Name = nameof(GetBestPost))]
    [ProducesResponseType(typeof(Envelope<BestPost>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetBestPost(string login) => Ok(await _userApiService.GetBestPost(login));
}