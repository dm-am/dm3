using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Blog.Blacklists;

/// <summary>
/// Blog blacklist management
/// </summary>
/// <remarks>
/// Allows blog owners to block users from commenting in their blogs.
/// </remarks>
[ApiController]
[Route("v1/blogs")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Blacklist")]
[EnableRateLimiting("default")]
public class BlogBlacklistController : ControllerBase
{
    private readonly IBlogBlacklistApiService _blacklistApiService;

    /// <inheritdoc />
    public BlogBlacklistController(IBlogBlacklistApiService blacklistApiService)
    {
        _blacklistApiService = blacklistApiService;
    }

    /// <summary>
    /// Get list of blacklisted users in blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <response code="200">Returns the list of blacklisted users for the blog</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to read blacklist of this blog</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("{id}/blacklist", Name = nameof(GetBlogBlacklist))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogBlacklist(Guid id) =>
        Ok(new ListEnvelope<User>(await _blacklistApiService.Get(id)));

    /// <summary>
    /// Add user to blog blacklist
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="request">User to blacklist</param>
    /// <response code="201">User successfully added to blacklist</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to blacklist users in this blog</response>
    /// <response code="404">Blog or user not found</response>
    /// <response code="409">User is already blacklisted</response>
    [HttpPost("{id}/blacklist", Name = nameof(AddToBlogBlacklist))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddToBlogBlacklist(Guid id, [FromBody] BlockUserRequest request)
    {
        var result = await _blacklistApiService.Create(id, request.Username);
        return CreatedAtRoute(nameof(GetBlogBlacklist), new { id }, result);
    }

    /// <summary>
    /// Remove user from blog blacklist
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="username">User's display name</param>
    /// <response code="204">User successfully removed from blacklist</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to un-blacklist users in this blog</response>
    /// <response code="404">Blog or user not found</response>
    /// <response code="409">User is not in the blacklist</response>
    [HttpDelete("{id}/blacklist/{username}", Name = nameof(RemoveFromBlogBlacklist))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveFromBlogBlacklist(Guid id, string username)
    {
        await _blacklistApiService.Delete(id, username);
        return NoContent();
    }
}
