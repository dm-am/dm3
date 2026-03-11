using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Users;

/// <summary>
/// Game users API
/// </summary>
/// <remarks>
/// Provides endpoints for viewing and managing game users (master, assistants, players, readers).
/// For invitations, see GameInvitationController.
/// </remarks>
[ApiController]
[Route("v1/games/{id}/users")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Users")]
public class GameUserController : ControllerBase
{
    private readonly IGameUserApiService _userApiService;

    /// <inheritdoc />
    public GameUserController(IGameUserApiService userApiService)
    {
        _userApiService = userApiService;
    }

    #region Users

    /// <summary>
    /// Get all users of a game
    /// </summary>
    /// <remarks>
    /// Returns users of the game. Use the role parameter to filter by specific roles:
    /// - master, assistant, mentor - game staff
    /// - player - users with active characters
    /// - applicant - users with pending characters only
    /// - formerPlayer - users with only inactive characters (no active or pending)
    /// - reader - subscribed users without other roles
    /// </remarks>
    /// <param name="id">Game ID</param>
    /// <param name="role">Optional role filter</param>
    /// <response code="200">List of users</response>
    /// <response code="404">Game not found</response>
    [HttpGet(Name = nameof(GetGameUsers))]
    [ProducesResponseType(typeof(ListEnvelope<GameUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameUsers(Guid id, [FromQuery] string? role = null)
    {
        var users = await _userApiService.GetUsers(id, role);
        return Ok(new ListEnvelope<GameUser>(users));
    }

    /// <summary>
    /// Remove assistant from the game by user ID
    /// </summary>
    /// <remarks>
    /// Only the game master can remove assistants.
    /// Players are removed by deleting their characters. Readers cannot be removed.
    /// </remarks>
    /// <param name="id">Game ID</param>
    /// <param name="userId">Assistant user ID to remove</param>
    /// <response code="204">Assistant removed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master</response>
    /// <response code="404">Game or assistant not found</response>
    [HttpDelete("{userId:guid}", Name = nameof(RemoveGameUser))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGameUser(Guid id, Guid userId)
    {
        await _userApiService.RemoveUser(id, userId);
        return NoContent();
    }

    #endregion

    #region Assistants

    /// <summary>
    /// Get list of game assistants
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <response code="200">List of assistants</response>
    /// <response code="404">Game not found</response>
    [HttpGet("assistants", Name = nameof(GetGameAssistants))]
    [ProducesResponseType(typeof(ListEnvelope<GameUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameAssistants(Guid id)
    {
        var assistants = await _userApiService.GetAssistants(id);
        return Ok(new ListEnvelope<GameUser>(assistants));
    }

    /// <summary>
    /// Remove assistant from game by username
    /// </summary>
    /// <param name="id">Game ID</param>
    /// <param name="username">Assistant username</param>
    /// <response code="204">Assistant removed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not game master</response>
    /// <response code="404">Game or assistant not found</response>
    [HttpDelete("assistants/{username}", Name = nameof(RemoveGameAssistant))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGameAssistant(Guid id, string username)
    {
        await _userApiService.RemoveAssistantByUsername(id, username);
        return NoContent();
    }

    #endregion

    // Note: Readers endpoints are at /v1/games/{id}/readers (see GameReaderController)
}
