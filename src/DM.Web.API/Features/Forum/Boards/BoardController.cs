using System.Threading.Tasks;
using DM.Web.API.Swagger;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Comments;
using DM.Web.API.Features.Forum.Moderators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Forum.Boards;

/// <summary>
/// Board management endpoints
/// </summary>
/// <remarks>
/// Provides access to individual forum boards and their content.
/// Each board is a thematic section containing topics and comments.
///
/// ## Available Operations
/// - Get all boards or a specific board
/// - Mark all comments on a board as read
/// - Get board moderators list
///
/// ## Access Control
/// Board visibility and topic creation permissions are controlled by board-level policies.
/// Some operations require authentication.
/// </remarks>
[ApiController]
[Route("v1/boards")]
[ApiExplorerSettings(GroupName = "Forum")]
[Tags("Forum Boards")]
public class BoardController : ControllerBase
{
    private readonly IBoardApiService _boardApiService;
    private readonly IForumCommentApiService _commentApiService;
    private readonly IBoardModeratorsApiService _moderatorsApiService;

    /// <summary>
    /// Creates a new instance of BoardController
    /// </summary>
    public BoardController(
        IBoardApiService boardApiService,
        IForumCommentApiService commentApiService,
        IBoardModeratorsApiService moderatorsApiService)
    {
        _boardApiService = boardApiService;
        _commentApiService = commentApiService;
        _moderatorsApiService = moderatorsApiService;
    }

    /// <summary>
    /// Get list of all boards
    /// </summary>
    /// <remarks>
    /// Returns all forum boards with their statistics:
    /// - Total topics and comments count
    /// - Unread topics and comments count (for authenticated users)
    /// - Last activity information
    ///
    /// Response is cached for 60 seconds.
    /// </remarks>
    /// <response code="200">List of all boards with statistics</response>
    [HttpGet(Name = nameof(GetBoards))]
    [ProducesResponseType(typeof(ListEnvelope<Board>), StatusCodes.Status200OK)]
    // No cache policy at all. The response carries per-user unread counters, and
    // a minute of browser-side freshness on those is a badge that does not clear
    // after DELETE /v1/boards/{id}/comments/unread — the very action whose whole
    // point is that it clears. "private" only moves the stale copy one hop
    // closer to the reader; it does not make it less stale.
    public async Task<IActionResult> GetBoards()
    {
        return Ok(await _boardApiService.GetBoards());
    }

    /// <summary>
    /// Get board details
    /// </summary>
    /// <remarks>
    /// Returns detailed information about a specific board including:
    /// - Board title and description
    /// - Topic and comment counts
    /// - Unread counts (for authenticated users)
    /// - Board moderators
    /// </remarks>
    /// <param name="id">Board identifier (GUID or URL slug)</param>
    /// <response code="200">Board details</response>
    /// <response code="404">Board not found</response>
    [HttpGet("{id}", Name = nameof(GetBoard))]
    [ProducesResponseType(typeof(Envelope<Board>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBoard(string id) => Ok(await _boardApiService.GetBoard(id));

    /// <summary>
    /// Mark all comments on board as read
    /// </summary>
    /// <remarks>
    /// Marks all comments in all topics within this board as read for the current user.
    /// Useful for clearing unread indicators for an entire board at once.
    /// </remarks>
    /// <param name="id">Board identifier (GUID or URL slug)</param>
    /// <response code="204">All comments marked as read</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Board not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(ReadBoardComments))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReadBoardComments(string id)
    {
        await _commentApiService.MarkAsRead(id);
        return NoContent();
    }

    /// <summary>
    /// Get board moderators
    /// </summary>
    /// <remarks>
    /// Returns list of users who moderate this specific board.
    /// Board moderators can edit and delete topics and comments within the board.
    /// </remarks>
    /// <param name="id">Board identifier (GUID or URL slug)</param>
    /// <response code="200">List of board moderators</response>
    /// <response code="404">Board not found</response>
    [HttpGet("{id}/moderators", Name = nameof(GetBoardModerators))]
    [ProducesResponseType(typeof(ListEnvelope<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBoardModerators(string id) => Ok(await _moderatorsApiService.GetModerators(id));

    /// <summary>
    /// Add board moderator
    /// </summary>
    /// <remarks>
    /// Assigns a user as a moderator of this board.
    /// Board moderators can edit and delete topics and comments within the board.
    /// Requires SeniorModerator or higher role.
    /// </remarks>
    /// <param name="id">Board identifier (GUID or URL slug)</param>
    /// <param name="username">Username to add as moderator</param>
    /// <response code="201">User added as moderator</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Board or user not found</response>
    /// <response code="409">User is already a moderator of this board</response>
    [HttpPost("{id}/moderators/{username}", Name = nameof(AddBoardModerator))]
    [AuthenticationRequired]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status201Created)]
    [CreatedWithoutLocation]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddBoardModerator(string id, string username)
    {
        var result = await _moderatorsApiService.AddModerator(id, username);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Remove board moderator
    /// </summary>
    /// <remarks>
    /// Removes a user from the list of board moderators.
    /// Requires SeniorModerator or higher role.
    /// </remarks>
    /// <param name="id">Board identifier (GUID or URL slug)</param>
    /// <param name="username">Username to remove from moderators</param>
    /// <response code="204">User removed from moderators</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Board or user not found, or user is not a moderator</response>
    [HttpDelete("{id}/moderators/{username}", Name = nameof(RemoveBoardModerator))]
    [AuthenticationRequired]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBoardModerator(string id, string username)
    {
        await _moderatorsApiService.RemoveModerator(id, username);
        return NoContent();
    }
}
