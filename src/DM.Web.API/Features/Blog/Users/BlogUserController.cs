using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Blog.Blogs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Blog.Users;

/// <summary>
/// Blog users API
/// </summary>
[ApiController]
[Route("v1/blogs/{id}/users")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Users")]
public class BlogUserController : ControllerBase
{
    private readonly BlogUserApiService _userApiService;
    private readonly BlogApiService _blogApiService;

    public BlogUserController(
        BlogUserApiService userApiService,
        BlogApiService blogApiService)
    {
        _userApiService = userApiService;
        _blogApiService = blogApiService;
    }

    /// <summary>
    /// Get all users of a blog
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="role">Optional role filter</param>
    /// <response code="200">List of users</response>
    /// <response code="404">Blog not found</response>
    [HttpGet(Name = nameof(GetBlogUsers))]
    [ProducesResponseType(typeof(ListEnvelope<BlogUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogUsers(string id, [FromQuery] BlogRole? role = null)
    {
        var blogId = await _blogApiService.ResolveId(id);
        var users = await _userApiService.GetUsers(blogId, role);
        return Ok(new ListEnvelope<BlogUser>(users));
    }

    /// <summary>
    /// Get list of blog assistants
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="200">List of assistants</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("assistants", Name = nameof(GetBlogAssistants))]
    [ProducesResponseType(typeof(ListEnvelope<BlogUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogAssistants(string id)
    {
        var blogId = await _blogApiService.ResolveId(id);
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBlogAssistant(string id, string username)
    {
        var blogId = await _blogApiService.ResolveId(id);
        await _userApiService.RemoveAssistantByUsername(blogId, username);
        return NoContent();
    }
}
