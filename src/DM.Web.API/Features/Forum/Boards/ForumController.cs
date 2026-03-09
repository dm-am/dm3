using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Forum.Comments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Forum.Boards;

/// <summary>
/// Forum management endpoints
/// </summary>
/// <remarks>
/// Provides access to forum boards and global forum operations.
/// The forum consists of multiple boards (sections) where users can create topics and discuss.
///
/// ## Available Operations
/// - Get list of all boards with statistics
/// - Mark all forum content as read (for authenticated users)
///
/// ## Caching
/// Board list is cached for 60 seconds for better performance.
/// </remarks>
[ApiController]
[Route("v1/forum")]
[ApiExplorerSettings(GroupName = "Forum")]
[Tags("Forum")]
public class ForumController : ControllerBase
{
    private readonly IBoardApiService _boardApiService;
    private readonly IForumCommentApiService _commentApiService;

    /// <summary>
    /// Creates a new instance of ForumController
    /// </summary>
    public ForumController(
        IBoardApiService boardApiService,
        IForumCommentApiService commentApiService)
    {
        _boardApiService = boardApiService;
        _commentApiService = commentApiService;
    }

    /// <summary>
    /// Get all forum boards
    /// </summary>
    /// <remarks>
    /// Returns a list of all forum boards (sections) with their statistics:
    /// - Total topics and comments count
    /// - Unread topics and comments count (for authenticated users)
    /// - Last comment information
    ///
    /// Response is cached for 60 seconds.
    /// </remarks>
    /// <response code="200">List of all boards</response>
    [HttpGet(Name = nameof(GetForum))]
    [ProducesResponseType(typeof(ListEnvelope<Board>), 200)]
    public async Task<IActionResult> GetForum()
    {
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(await _boardApiService.GetBoards());
    }

    /// <summary>
    /// Mark all forum comments as read
    /// </summary>
    /// <remarks>
    /// Marks all comments across all boards as read for the current user.
    /// Useful for clearing all unread indicators at once.
    /// </remarks>
    /// <response code="204">All comments marked as read</response>
    /// <response code="401">User must be authenticated</response>
    [HttpDelete("comments/unread", Name = nameof(ReadAllForumComments))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> ReadAllForumComments()
    {
        await _commentApiService.MarkAllAsRead();
        return NoContent();
    }
}
