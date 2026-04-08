using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Blog.Blogs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Blog.Users;

/// <summary>
/// Blog users API
/// </summary>
/// <remarks>
/// Provides endpoints for viewing and managing blog users (owner, assistants, readers).
/// For invitations, see BlogInvitationController.
/// </remarks>
[ApiController]
[Route("v1/blogs/{id}/users")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Users")]
public class BlogUserController : ControllerBase
{
    private readonly IBlogUserApiService _userApiService;
    private readonly IBlogApiService _blogApiService;

    /// <inheritdoc />
    public BlogUserController(
        IBlogUserApiService userApiService,
        IBlogApiService blogApiService)
    {
        _userApiService = userApiService;
        _blogApiService = blogApiService;
    }

    private async Task<Guid> ResolveBlogId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : (await _blogApiService.GetByPublicId(id)).Resource.Id;

    #region Users

    /// <summary>
    /// Get all users of a blog
    /// </summary>
    /// <remarks>
    /// Returns users of the blog. Use the role parameter to filter by specific roles:
    /// - owner - blog owner
    /// - assistant - blog assistants
    /// - mentor - assigned mentor
    /// - reader - subscribed users
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="role">Optional role filter</param>
    /// <response code="200">List of users</response>
    /// <response code="404">Blog not found</response>
    [HttpGet(Name = nameof(GetBlogUsers))]
    [ProducesResponseType(typeof(ListEnvelope<BlogUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogUsers(string id, [FromQuery] string? role = null)
    {
        var blogId = await ResolveBlogId(id);
        var users = await _userApiService.GetUsers(blogId, role);
        return Ok(new ListEnvelope<BlogUser>(users));
    }

    /// <summary>
    /// Remove assistant from the blog by user ID
    /// </summary>
    /// <remarks>
    /// Only the blog owner can remove assistants.
    /// Readers can only unsubscribe themselves. Cannot remove the blog owner.
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="userId">Assistant user ID to remove</param>
    /// <response code="204">Assistant removed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not blog owner</response>
    /// <response code="404">Blog or assistant not found</response>
    [HttpDelete("{userId:guid}", Name = nameof(RemoveBlogUser))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBlogUser(string id, Guid userId)
    {
        var blogId = await ResolveBlogId(id);
        await _userApiService.RemoveUser(blogId, userId);
        return NoContent();
    }

    #endregion

    #region Assistants

    /// <summary>
    /// Get list of blog assistants
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="200">List of assistants</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("assistants", Name = nameof(GetBlogAssistants))]
    [ProducesResponseType(typeof(ListEnvelope<BlogUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogAssistants(string id)
    {
        var blogId = await ResolveBlogId(id);
        var assistants = await _userApiService.GetAssistants(blogId);
        return Ok(new ListEnvelope<BlogUser>(assistants));
    }

    /// <summary>
    /// Remove assistant from blog by username
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="username">Assistant username</param>
    /// <response code="204">Assistant removed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not blog owner</response>
    /// <response code="404">Blog or assistant not found</response>
    [HttpDelete("assistants/{username}", Name = nameof(RemoveBlogAssistant))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBlogAssistant(string id, string username)
    {
        var blogId = await ResolveBlogId(id);
        await _userApiService.RemoveAssistantByUsername(blogId, username);
        return NoContent();
    }

    #endregion

    // Note: Readers endpoints are at /v1/blogs/{id}/readers (see BlogReaderController)
}
