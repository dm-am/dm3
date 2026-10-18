using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Unread;

/// <summary>
/// Controller for finding first unread content in games
/// </summary>
[ApiController]
[Route("v1/games")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Game Unread")]
public class UnreadController : ControllerBase
{
    private readonly IUnreadApiService _unreadApiService;
    private readonly IGameApiService _gameApiService;

    /// <inheritdoc />
    public UnreadController(IUnreadApiService unreadApiService, IGameApiService gameApiService)
    {
        _unreadApiService = unreadApiService;
        _gameApiService = gameApiService;
    }

    /// <summary>
    /// Get the first unread post in a game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the first unread post location</response>
    /// <response code="404">Game not found</response>
    /// <remarks>
    /// For anonymous users, returns the first post of the first room.
    /// For authenticated users with unread posts, returns the first unread post.
    /// For authenticated users with no unread posts, returns the last post of the last room.
    /// </remarks>
    [HttpGet("{id}/posts/first-unread", Name = nameof(GetFirstUnreadPost))]
    [ProducesResponseType(typeof(Envelope<FirstUnreadPostResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFirstUnreadPost(string id) =>
        Ok(await _unreadApiService.GetFirstUnreadPost(await _gameApiService.ResolveId(id)));

    /// <summary>
    /// Get the first unread post in a room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <param name="take">Page size to count the page at; the reader's own posts-per-page setting when omitted</param>
    /// <response code="200">Returns the first unread post location and its page</response>
    /// <response code="400">Page size out of range</response>
    /// <response code="404">Room not found</response>
    /// <remarks>
    /// Where the room opens when no page is named.
    /// For anonymous users, returns the first post of the room.
    /// For authenticated users with unread posts, returns the first unread post.
    /// Unread is what was written after both the room was last marked read and the
    /// reader's own last post in it: a reader's post is not news to them.
    /// For authenticated users with no unread posts, returns the last post, on the last page.
    /// A room without posts answers postId null, postNumber 0, page 1.
    /// </remarks>
    [HttpGet("~/v1/rooms/{id}/posts/first-unread", Name = nameof(GetFirstUnreadPostInRoom))]
    [ProducesResponseType(typeof(Envelope<FirstUnreadRoomPostResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFirstUnreadPostInRoom(
        Guid id,
        // The ceiling is the one every page of posts is served at (PagingQuery).
        [FromQuery][Range(1, 200, ErrorMessage = "Размер страницы от 1 до 200")] int? take = null) =>
        Ok(await _unreadApiService.GetFirstUnreadPostInRoom(id, take));

    /// <summary>
    /// Get the first unread comment in a game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the first unread comment location</response>
    /// <response code="404">Game not found</response>
    /// <remarks>
    /// For anonymous users, returns the first comment.
    /// For authenticated users with unread comments, returns the first unread comment.
    /// For authenticated users with no unread comments, returns the last comment.
    /// </remarks>
    [HttpGet("{id}/comments/first-unread", Name = nameof(GetFirstUnreadComment))]
    [ProducesResponseType(typeof(Envelope<FirstUnreadCommentResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFirstUnreadComment(string id) =>
        Ok(await _unreadApiService.GetFirstUnreadComment(await _gameApiService.ResolveId(id)));
}
