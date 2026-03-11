using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IFeaturedPostsService = DM.Domain.Game.Features.Posts.IFeaturedPostsService;
using FeaturedPostsEnvelope = DM.Domain.Game.Features.Posts.FeaturedPostsEnvelope;

namespace DM.Web.API.Features.Game.Posts;

/// <inheritdoc />
[ApiController]
[Route("v1/posts")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Posts")]
public class PostController : ControllerBase
{
    private readonly IPostApiService _postApiService;
    private readonly IFeaturedPostsService _featuredPostsService;

    /// <inheritdoc />
    public PostController(
        IPostApiService postApiService,
        IFeaturedPostsService featuredPostsService)
    {
        _postApiService = postApiService;
        _featuredPostsService = featuredPostsService;
    }

    /// <summary>
    /// Get list of posts in room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">Returns the list of posts in the room</response>
    /// <response code="404">Room not found</response>
    [HttpGet("~/v1/rooms/{id}/posts", Name = nameof(GetPosts))]
    [ProducesResponseType(typeof(ListEnvelope<Post>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosts(Guid id, [FromQuery] PagingQuery q) =>
        Ok(await _postApiService.Get(id, q));

    /// <summary>
    /// Create new post in room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <param name="post">Post details</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of post parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create post in this room</response>
    /// <response code="404">Room not found</response>
    [HttpPost("~/v1/rooms/{id}/posts", Name = nameof(PostPost))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Post>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostPost(Guid id, [FromBody] CreatePostRequest post)
    {
        var result = await _postApiService.Create(id, post);
        return CreatedAtRoute(nameof(GetPost),
            new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Get post
    /// </summary>
    /// <param name="id">Post identifier</param>
    /// <response code="200">Returns the post details</response>
    /// <response code="404">Post not found</response>
    [HttpGet("{id}", Name = nameof(GetPost))]
    [ProducesResponseType(typeof(Envelope<Post>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPost(Guid id) => Ok(await _postApiService.Get(id));

    /// <summary>
    /// Update post
    /// </summary>
    /// <param name="id">Post identifier</param>
    /// <param name="post">Updated post details</param>
    /// <response code="200">Returns the updated post</response>
    /// <response code="400">Some of post changed properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change some properties of this post</response>
    /// <response code="404">Post not found</response>
    [HttpPatch("{id}", Name = nameof(PatchPost))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Post>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchPost(Guid id, [FromBody] Post post) =>
        Ok(await _postApiService.Update(id, post));

    /// <summary>
    /// Delete post
    /// </summary>
    /// <param name="id">Post identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the post</response>
    /// <response code="404">Post not found</response>
    [HttpDelete("{id}", Name = nameof(DeletePost))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        await _postApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Get featured posts (best of week and last with plus)
    /// </summary>
    /// <response code="200">Returns featured posts</response>
    [HttpGet("featured", Name = nameof(GetFeaturedPosts))]
    [ProducesResponseType(typeof(FeaturedPostsEnvelope), StatusCodes.Status200OK)]
    [ResponseCache(Duration = 300)] // 5 minutes cache
    public async Task<IActionResult> GetFeaturedPosts()
    {
        var result = await _featuredPostsService.Get();
        return Ok(result);
    }
}
