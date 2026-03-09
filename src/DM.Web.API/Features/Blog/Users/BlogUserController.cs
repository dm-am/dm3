using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
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

    /// <inheritdoc />
    public BlogUserController(IBlogUserApiService userApiService)
    {
        _userApiService = userApiService;
    }

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
    /// <param name="id">Blog ID</param>
    /// <param name="role">Optional role filter</param>
    /// <response code="200">List of users</response>
    /// <response code="404">Blog not found</response>
    [HttpGet(Name = nameof(GetBlogUsers))]
    [ProducesResponseType(typeof(ListEnvelope<BlogUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogUsers(Guid id, [FromQuery] string? role = null)
    {
        var users = await _userApiService.GetUsers(id, role);
        return Ok(new ListEnvelope<BlogUser>(users));
    }

    /// <summary>
    /// Remove assistant from the blog by user ID
    /// </summary>
    /// <remarks>
    /// Only the blog owner can remove assistants.
    /// Readers can only unsubscribe themselves. Cannot remove the blog owner.
    /// </remarks>
    /// <param name="id">Blog ID</param>
    /// <param name="userId">Assistant user ID to remove</param>
    /// <response code="204">Assistant removed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not blog owner</response>
    /// <response code="404">Blog or assistant not found</response>
    [HttpDelete("{userId:guid}", Name = nameof(RemoveBlogUser))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBlogUser(Guid id, Guid userId)
    {
        await _userApiService.RemoveUser(id, userId);
        return NoContent();
    }

    #endregion

    #region Assistants

    /// <summary>
    /// Get list of blog assistants
    /// </summary>
    /// <param name="id">Blog ID</param>
    /// <response code="200">List of assistants</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("assistants", Name = nameof(GetBlogAssistants))]
    [ProducesResponseType(typeof(ListEnvelope<BlogUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogAssistants(Guid id)
    {
        var assistants = await _userApiService.GetAssistants(id);
        return Ok(new ListEnvelope<BlogUser>(assistants));
    }

    /// <summary>
    /// Remove assistant from blog by username
    /// </summary>
    /// <param name="id">Blog ID</param>
    /// <param name="username">Assistant username</param>
    /// <response code="204">Assistant removed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not blog owner</response>
    /// <response code="404">Blog or assistant not found</response>
    [HttpDelete("assistants/{username}", Name = nameof(RemoveBlogAssistant))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBlogAssistant(Guid id, string username)
    {
        await _userApiService.RemoveAssistantByUsername(id, username);
        return NoContent();
    }

    #endregion

    // Note: Readers endpoints are at /v1/blogs/{id}/readers (see BlogReaderController)
}
