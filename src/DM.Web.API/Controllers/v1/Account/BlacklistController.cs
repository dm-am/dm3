using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Blacklist;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Blacklist;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// API controller for managing personal blacklist
/// </summary>
/// <remarks>
/// Allows users to block other users to hide their content and block messages.
/// </remarks>
[ApiController]
[Route("v1/account/blacklist")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Blacklist")]
public class BlacklistController : ControllerBase
{
    private readonly IBlacklistApiService _apiService;

    /// <inheritdoc />
    public BlacklistController(IBlacklistApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get my blacklist
    /// </summary>
    /// <remarks>
    /// Returns list of users blocked by the current user.
    /// </remarks>
    /// <response code="200">List of blocked users</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetMyBlacklist))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<BlacklistEntry>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetMyBlacklist() =>
        Ok(await _apiService.GetMyBlacklist());

    /// <summary>
    /// Get blacklist settings
    /// </summary>
    /// <remarks>
    /// Returns behavior settings for blocked users.
    /// </remarks>
    /// <response code="200">Blacklist settings</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("settings", Name = nameof(GetBlacklistSettings))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<UserBlacklistSettings>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetBlacklistSettings() =>
        Ok(await _apiService.GetSettings());

    /// <summary>
    /// Update blacklist settings
    /// </summary>
    /// <remarks>
    /// Updates behavior settings for blocked users.
    /// </remarks>
    /// <param name="settings">New settings</param>
    /// <response code="200">Updated settings</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPatch("settings", Name = nameof(UpdateBlacklistSettings))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<UserBlacklistSettings>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> UpdateBlacklistSettings([FromBody] UserBlacklistSettings settings) =>
        Ok(await _apiService.UpdateSettings(settings));

    /// <summary>
    /// Block a user
    /// </summary>
    /// <remarks>
    /// Adds a user to the blacklist. If already blocked, returns existing entry.
    /// </remarks>
    /// <param name="request">Block request with user login</param>
    /// <response code="201">User blocked</response>
    /// <response code="400">Cannot block yourself</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">User not found</response>
    [HttpPost(Name = nameof(BlockUser))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<BlacklistEntry>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest request)
    {
        var result = await _apiService.BlockUser(request);
        return CreatedAtRoute(nameof(GetMyBlacklist), result);
    }

    /// <summary>
    /// Unblock a user
    /// </summary>
    /// <remarks>
    /// Removes a user from the blacklist.
    /// </remarks>
    /// <param name="login">User login to unblock</param>
    /// <response code="204">User unblocked</response>
    /// <response code="401">User must be authenticated</response>
    [HttpDelete("{login}", Name = nameof(UnblockUser))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> UnblockUser(string login)
    {
        await _apiService.UnblockUser(login);
        return NoContent();
    }
}
