using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Community;

/// <inheritdoc />
[ApiController]
[Route("v1/users")]
[ApiExplorerSettings(GroupName = "Community")]
public class UserController : ControllerBase
{
    private readonly IUserApiService _userApiService;

    /// <inheritdoc />
    public UserController(
        IUserApiService userApiService)
    {
        _userApiService = userApiService;
    }

    /// <summary>
    /// Get list of activated users
    /// </summary>
    /// <response code="200"></response>
    [HttpGet("")]
    [ProducesResponseType(typeof(ListEnvelope<User>), 200)]
    public async Task<IActionResult> GetUsers([FromQuery] UsersQuery query) =>
        Ok(await _userApiService.GetUsers(query));

    /// <summary>
    /// Get users by role
    /// </summary>
    /// <param name="role">User role</param>
    /// <response code="200"></response>
    [HttpGet("by-role/{role}", Name = nameof(GetUsersByRole))]
    [ProducesResponseType(typeof(ListEnvelope<User>), 200)]
    public async Task<IActionResult> GetUsersByRole(UserRole role) =>
        Ok(await _userApiService.GetUsersByRole(role));

    /// <summary>
    /// Get current authenticated user
    /// </summary>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("me", Name = nameof(GetCurrentUser))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetCurrentUser() => Ok(await _userApiService.GetCurrentUser());

    /// <summary>
    /// Get user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <response code="200"></response>
    /// <response code="410">User not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetUserById))]
    [ProducesResponseType(typeof(Envelope<User>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetUserById(Guid id) => Ok(await _userApiService.GetUser(id));

    /// <summary>
    /// Get user by login
    /// </summary>
    /// <param name="login"></param>
    /// <response code="200"></response>
    /// <response code="410">User not found</response>
    [HttpGet("by-login/{login}", Name = nameof(GetUserByLogin))]
    [ProducesResponseType(typeof(Envelope<User>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetUserByLogin(string login) => Ok(await _userApiService.GetUser(login));

    /// <summary>
    /// Get user (legacy endpoint, prefer by-login/{login})
    /// </summary>
    /// <param name="login"></param>
    /// <response code="200"></response>
    /// <response code="410">User not found</response>
    [HttpGet("{login}", Name = nameof(GetUser))]
    [ProducesResponseType(typeof(Envelope<User>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetUser(string login) => Ok(await _userApiService.GetUser(login));

    /// <summary>
    /// Get user details by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <response code="200"></response>
    /// <response code="410">User not found</response>
    [HttpGet("{id:guid}/details", Name = nameof(GetUserDetailsById))]
    [ProducesResponseType(typeof(Envelope<UserDetails>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetUserDetailsById(Guid id) => Ok(await _userApiService.GetUserDetails(id));

    /// <summary>
    /// Get user details by login
    /// </summary>
    /// <param name="login"></param>
    /// <response code="200"></response>
    /// <response code="410">User not found</response>
    [HttpGet("{login}/details", Name = nameof(GetUserDetails))]
    [ProducesResponseType(typeof(Envelope<UserDetails>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetUserDetails(string login) => Ok(await _userApiService.GetUserDetails(login));

    /// <summary>
    /// Update user details
    /// </summary>
    /// <param name="login"></param>
    /// <param name="user"></param>
    /// <response code="200"></response>
    /// <response code="400">Some parameters were incorrect</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to modify this user</response>
    /// <response code="410">User not found</response>
    [HttpPatch("{login}/details", Name = nameof(PatchUserDetails))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<UserDetails>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PatchUserDetails(string login, [FromBody] UserDetails user) =>
        Ok(await _userApiService.UpdateUser(login, user));

    /// <summary>
    /// Get user settings
    /// </summary>
    /// <param name="login"></param>
    /// <response code="200"></response>
    /// <response code="410">User not found</response>
    [HttpGet("{login}/settings", Name = nameof(GetUserSettings))]
    [ProducesResponseType(typeof(Envelope<UserSettings>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetUserSettings(string login) =>
        Ok(await _userApiService.GetUserSettings(login));

    /// <summary>
    /// Update user settings
    /// </summary>
    /// <param name="login"></param>
    /// <param name="settings"></param>
    /// <response code="200"></response>
    /// <response code="400">Some parameters were incorrect</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to modify this user</response>
    /// <response code="410">User not found</response>
    [HttpPatch("{login}/settings", Name = nameof(PatchUserSettings))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<UserSettings>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PatchUserSettings(string login, [FromBody] UserSettings settings) =>
        Ok(await _userApiService.UpdateUserSettings(login, settings));
}