using System;
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
