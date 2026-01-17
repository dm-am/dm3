using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Boards;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Boards;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Forums;

/// <inheritdoc />
[ApiController]
[Route("v1/boards")]
[ApiExplorerSettings(GroupName = "Forum")]
public class BoardController : ControllerBase
{
    private readonly IForumApiService _forumApiService;
    private readonly ITopicApiService _topicApiService;
    private readonly ICommentApiService _commentApiService;
    private readonly IModeratorsApiService _moderatorsApiService;

    /// <inheritdoc />
    public BoardController(
        IForumApiService forumApiService,
        ITopicApiService topicApiService,
        ICommentApiService commentApiService,
        IModeratorsApiService moderatorsApiService)
    {
        _forumApiService = forumApiService;
        _topicApiService = topicApiService;
        _commentApiService = commentApiService;
        _moderatorsApiService = moderatorsApiService;
    }

    /// <summary>
    /// Get list of all boards
    /// </summary>
    /// <response code="200"></response>
    [HttpGet(Name = nameof(GetBoards))]
    [ProducesResponseType(typeof(ListEnvelope<Board>), 200)]
    public async Task<IActionResult> GetBoards()
    {
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(await _forumApiService.GetBoards());
    }

    /// <summary>
    /// Get board
    /// </summary>
    /// <param name="id">Board id</param>
    /// <response code="200"></response>
    /// <response code="410">Board not found</response>
    [HttpGet("{id}", Name = nameof(GetBoard))]
    [ProducesResponseType(typeof(Envelope<Board>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetBoard(string id) => Ok(await _forumApiService.GetBoard(id));

    /// <summary>
    /// Mark all comments on board as read
    /// </summary>
    /// <param name="id">Board id</param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Board not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(ReadBoardComments))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> ReadBoardComments(string id)
    {
        await _commentApiService.MarkAsRead(id);
        return NoContent();
    }

    /// <summary>
    /// Get board moderators
    /// </summary>
    /// <param name="id">Board id</param>
    /// <response code="200"></response>
    /// <response code="410">Board not found</response>
    [HttpGet("{id}/moderators", Name = nameof(GetBoardModerators))]
    [ProducesResponseType(typeof(ListEnvelope<User>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetBoardModerators(string id) => Ok(await _moderatorsApiService.GetModerators(id));
}