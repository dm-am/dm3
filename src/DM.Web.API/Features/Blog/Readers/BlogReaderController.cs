using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Blog.Users;
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
[Route("v1/blogs/{id:guid}/readers")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Readers")]
public class BlogReaderController : ControllerBase
{
    private readonly IBlogUserApiService _userApiService;

    /// <inheritdoc />
    public BlogReaderController(IBlogUserApiService userApiService)
    {
        _userApiService = userApiService;
    }

    /// <summary>
    /// Get list of blog readers
    /// </summary>
    /// <remarks>
    /// Returns users subscribed to the blog without other roles.
    /// </remarks>
    /// <param name="id">Blog ID</param>
    /// <response code="200">List of readers</response>
    /// <response code="404">Blog not found</response>
    [HttpGet(Name = nameof(GetBlogReaders))]
    [ProducesResponseType(typeof(ListEnvelope<BlogUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogReaders(Guid id)
    {
        var readers = await _userApiService.GetReaders(id);
        return Ok(new ListEnvelope<BlogUser>(readers));
    }

    /// <summary>
    /// Subscribe to the blog (become a reader)
    /// </summary>
    /// <remarks>
    /// Adds current user as a blog reader (subscriber).
    /// </remarks>
    /// <param name="id">Blog ID</param>
    /// <response code="201">Successfully subscribed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Draft blog with private visibility requires invitation</response>
    /// <response code="404">Blog not found</response>
    /// <response code="409">Already subscribed</response>
    [HttpPost(Name = nameof(SubscribeToBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(BlogUser), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubscribeToBlog(Guid id)
    {
        var reader = await _userApiService.Subscribe(id);
        return CreatedAtRoute(nameof(GetBlogReaders), new { id }, reader);
    }

    /// <summary>
    /// Unsubscribe from the blog (leave as reader)
    /// </summary>
    /// <param name="id">Blog ID</param>
    /// <response code="204">Successfully unsubscribed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Blog not found or not subscribed</response>
    [HttpDelete(Name = nameof(UnsubscribeFromBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsubscribeFromBlog(Guid id)
    {
        await _userApiService.Unsubscribe(id);
        return NoContent();
    }

    // Note: DELETE /{username} is not provided - readers can only unsubscribe themselves
    // Use blacklist to prevent problematic users from accessing content
}
