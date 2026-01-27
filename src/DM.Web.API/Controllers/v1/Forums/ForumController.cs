using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Boards;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Boards;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Forums;

/// <inheritdoc />
[ApiController]
[Route("v1/forum")]
[ApiExplorerSettings(GroupName = "Forum")]
public class ForumController : ControllerBase
{
    private readonly IBoardApiService _boardApiService;
    private readonly ICommentApiService _commentApiService;

    /// <inheritdoc />
    public ForumController(
        IBoardApiService boardApiService,
        ICommentApiService commentApiService)
    {
        _boardApiService = boardApiService;
        _commentApiService = commentApiService;
    }

    /// <summary>
    /// Get forum (list of all boards)
    /// </summary>
    /// <response code="200"></response>
    [HttpGet(Name = nameof(GetForum))]
    [ProducesResponseType(typeof(ListEnvelope<Board>), 200)]
    public async Task<IActionResult> GetForum()
    {
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(await _boardApiService.GetBoards());
    }

    /// <summary>
    /// Mark all comments on forum as read
    /// </summary>
    /// <response code="204"></response>
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
