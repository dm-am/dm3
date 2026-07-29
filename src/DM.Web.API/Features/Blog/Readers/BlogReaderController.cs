using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Blog.Users;
using DM.Web.API.Features.Blog.Blogs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Blog.Readers;

/// <summary>
/// Blog readers (subscribers) API
/// </summary>
/// <remarks>
/// Provides endpoints for viewing and managing blog readers (subscribers).
/// Readers are users subscribed to the blog for updates without contributing as assistants.
/// </remarks>
[ApiController]
[Route("v1/blogs/{id}/readers")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Readers")]
public class BlogReaderController : ControllerBase
{
    private readonly IBlogUserApiService _userApiService;
    private readonly IBlogApiService _blogApiService;

    /// <inheritdoc />
    public BlogReaderController(
        IBlogUserApiService userApiService,
        IBlogApiService blogApiService)
    {
        _userApiService = userApiService;
        _blogApiService = blogApiService;
    }

    private async Task<Guid> ResolveBlogId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : (await _blogApiService.GetByPublicId(id)).Resource.Id;

    /// <summary>
    /// Get list of blog readers
    /// </summary>
    /// <remarks>
    /// Returns users subscribed to the blog without other roles.
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="200">List of readers</response>
    /// <response code="404">Blog not found</response>
    [HttpGet(Name = nameof(GetBlogReaders))]
    [ProducesResponseType(typeof(ListEnvelope<BlogUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogReaders(string id)
    {
        var blogId = await ResolveBlogId(id);
        var readers = await _userApiService.GetReaders(blogId);
        return Ok(new ListEnvelope<BlogUser>(readers));
    }

    /// <summary>
    /// Subscribe to the blog (become a reader)
    /// </summary>
    /// <remarks>
    /// Adds current user as a blog reader (subscriber).
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="201">Successfully subscribed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Draft blog with private visibility requires invitation</response>
    /// <response code="404">Blog not found</response>
    /// <response code="409">Already subscribed</response>
    [HttpPost(Name = nameof(SubscribeToBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(BlogUser), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubscribeToBlog(string id)
    {
        var blogId = await ResolveBlogId(id);
        var reader = await _userApiService.Subscribe(blogId);
        return StatusCode(StatusCodes.Status201Created, reader);
    }

    /// <summary>
    /// Unsubscribe from the blog (leave as reader)
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="204">Successfully unsubscribed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Blog not found or not subscribed</response>
    [HttpDelete(Name = nameof(UnsubscribeFromBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsubscribeFromBlog(string id)
    {
        var blogId = await ResolveBlogId(id);
        await _userApiService.Unsubscribe(blogId);
        return NoContent();
    }

    // Note: DELETE /{username} is not provided - readers can only unsubscribe themselves
    // Use blacklist to prevent problematic users from accessing content
}
