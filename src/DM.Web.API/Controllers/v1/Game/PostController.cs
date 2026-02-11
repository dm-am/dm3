using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Game.BusinessProcesses.Posts.Featured;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;
using DM.Web.API.Services.Game;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Game;

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
    /// <response code="410">Room not found</response>
    [HttpGet("~/v1/rooms/{id}/posts", Name = nameof(GetPosts))]
    [ProducesResponseType(typeof(ListEnvelope<Post>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    /// <response code="410">Room not found</response>
    [HttpPost("~/v1/rooms/{id}/posts", Name = nameof(PostPost))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Post>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    /// <response code="410">Post not found</response>
    [HttpGet("{id}", Name = nameof(GetPost))]
    [ProducesResponseType(typeof(Envelope<Post>), 200)]
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
    /// <response code="410">Post not found</response>
    [HttpPatch("{id}", Name = nameof(PatchPost))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Post>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PatchPost(Guid id, [FromBody] Post post) =>
        Ok(await _postApiService.Update(id, post));

    /// <summary>
    /// Delete post
    /// </summary>
    /// <param name="id">Post identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the post</response>
    /// <response code="410">Post not found</response>
    [HttpDelete("{id}", Name = nameof(DeletePost))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [ProducesResponseType(typeof(DM.Services.Game.Dto.Output.FeaturedPostsEnvelope), 200)]
    [ResponseCache(Duration = 300)] // 5 minutes cache
    public async Task<IActionResult> GetFeaturedPosts()
    {
        var result = await _featuredPostsService.Get();
        return Ok(result);
    }
}
