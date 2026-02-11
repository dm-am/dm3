using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Boards;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Boards;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Forum;

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
    private readonly ITopicApiService _topicApiService;
    private readonly ICommentApiService _commentApiService;
    private readonly IModeratorsApiService _moderatorsApiService;

    /// <summary>
    /// Creates a new instance of BoardController
    /// </summary>
    public BoardController(
        IBoardApiService boardApiService,
        ITopicApiService topicApiService,
        ICommentApiService commentApiService,
        IModeratorsApiService moderatorsApiService)
    {
        _boardApiService = boardApiService;
        _topicApiService = topicApiService;
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
    [ProducesResponseType(typeof(ListEnvelope<Board>), 200)]
    public async Task<IActionResult> GetBoards()
    {
        Response.Headers.CacheControl = "public, max-age=60";
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
    /// <response code="410">Board not found</response>
    [HttpGet("{id}", Name = nameof(GetBoard))]
    [ProducesResponseType(typeof(Envelope<Board>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    /// <response code="410">Board not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(ReadBoardComments))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    /// <response code="410">Board not found</response>
    [HttpGet("{id}/moderators", Name = nameof(GetBoardModerators))]
    [ProducesResponseType(typeof(ListEnvelope<User>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetBoardModerators(string id) => Ok(await _moderatorsApiService.GetModerators(id));
}