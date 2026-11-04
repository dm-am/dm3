using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Features.Forum.Comments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Forum.Boards;

/// <summary>
/// Forum management endpoints
/// </summary>
[ApiController]
[Route("v1/forum")]
[ApiExplorerSettings(GroupName = "Forum")]
[Tags("Forum")]
public class ForumController : ControllerBase
{
    private readonly ForumCommentApiService _commentApiService;

    public ForumController(
        ForumCommentApiService commentApiService)
    {
        _commentApiService = commentApiService;
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReadAllForumComments()
    {
        await _commentApiService.MarkAllAsRead();
        return NoContent();
    }
}
