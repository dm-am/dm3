using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;

namespace DM.Web.API.Features.Personal.Blacklists;

/// <summary>
/// Personal blacklist management
/// </summary>
/// <remarks>
/// Allows users to block other users to hide their content and block messages.
/// </remarks>
[ApiController]
[Route("v1/users/me/blacklist")]
[ApiExplorerSettings(GroupName = "Personal")]
[Tags("Blacklist")]
[AuthenticationRequired]
[EnableRateLimiting(RateLimitPolicies.Default)]
public class BlacklistController : ControllerBase
{
    private readonly IUserBlacklistApiService _apiService;

    /// <inheritdoc />
    public BlacklistController(IUserBlacklistApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get my blacklist
    /// </summary>
    /// <remarks>
    /// Returns list of users blocked by the current user with pagination.
    /// </remarks>
    /// <param name="query">Pagination parameters</param>
    /// <response code="200">List of blocked users</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetMyBlacklist))]
    [ProducesResponseType(typeof(ListEnvelope<BlacklistEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyBlacklist([FromQuery] PagingQuery query)
    {
        var (entries, paging) = await _apiService.GetMyBlacklist(query);
        return Ok(new ListEnvelope<BlacklistEntry>(entries, paging));
    }

    /// <summary>
    /// Get blacklist settings
    /// </summary>
    /// <remarks>
    /// Returns behavior settings for blocked users.
    /// </remarks>
    /// <response code="200">Blacklist settings</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("settings", Name = nameof(GetBlacklistSettings))]
    [ProducesResponseType(typeof(BlacklistSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBlacklistSettings() =>
        Ok(await _apiService.GetSettings());

    /// <summary>
    /// Update blacklist settings
    /// </summary>
    /// <remarks>
    /// Updates behavior settings for blocked users.
    /// </remarks>
    /// <param name="request">Flags to change</param>
    /// <response code="200">Updated settings</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPatch("settings", Name = nameof(UpdateBlacklistSettings))]
    [ProducesResponseType(typeof(BlacklistSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateBlacklistSettings([FromBody] UpdateBlacklistSettingsRequest request) =>
        Ok(await _apiService.UpdateSettings(request));

    /// <summary>
    /// Block a user
    /// </summary>
    /// <remarks>
    /// Adds a user to the blacklist. If already blocked, returns existing entry.
    /// </remarks>
    /// <param name="request">Block request with username</param>
    /// <response code="201">User blocked</response>
    /// <response code="400">Cannot block yourself</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">User not found</response>
    [HttpPost(Name = nameof(BlockUser))]
    [ProducesResponseType(typeof(BlacklistEntry), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest request)
    {
        var result = await _apiService.BlockUser(request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Unblock a user
    /// </summary>
    /// <remarks>
    /// Removes a user from the blacklist.
    /// </remarks>
    /// <param name="username">Username of user to unblock</param>
    /// <response code="204">User unblocked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">User not found in blacklist</response>
    [HttpDelete("{username}", Name = nameof(UnblockUser))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnblockUser(string username)
    {
        await _apiService.UnblockUser(username);
        return NoContent();
    }
}
