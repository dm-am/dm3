using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Blacklists;

/// <inheritdoc />
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Game Blacklist")]
public class GameBlacklistController : ControllerBase
{
    private readonly IBlacklistApiService _blacklistApiService;

    /// <inheritdoc />
    public GameBlacklistController(IBlacklistApiService blacklistApiService)
    {
        _blacklistApiService = blacklistApiService;
    }

    /// <summary>
    /// Get list of blacklisted users in game
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="200">Returns the list of blacklisted users for the game</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to read blacklist of this game</response>
    /// <response code="404">Game not found</response>
    [HttpGet("{id}/blacklist", Name = nameof(GetBlacklist))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlacklist(Guid id) => Ok(await _blacklistApiService.Get(id));

    /// <summary>
    /// Add new blacklisted user in game
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <param name="user">User to blacklist</param>
    /// <response code="201">User successfully added to blacklist</response>
    /// <response code="400">Some user properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to blacklist users in this game</response>
    /// <response code="409">User is already blacklisted</response>
    /// <response code="404">Game not found</response>
    [HttpPost("{id}/blacklist", Name = nameof(PostBlacklist))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostBlacklist(Guid id, [FromBody] User user)
    {
        var result = await _blacklistApiService.Create(id, user);
        return CreatedAtRoute(nameof(GetBlacklist), new {id}, result);
    }

    /// <summary>
    /// Delete blacklisted user in game
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <param name="login">User login</param>
    /// <response code="204">User successfully removed from blacklist</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to un-blacklist users in this game</response>
    /// <response code="409">User is not in the blacklist</response>
    /// <response code="404">Game not found</response>
    [HttpDelete("{id}/blacklist/{login}", Name = nameof(DeleteBlacklist))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBlacklist(Guid id, string login)
    {
        await _blacklistApiService.Delete(id, login);
        return NoContent();
    }
}
