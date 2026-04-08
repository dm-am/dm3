using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Blog.Blogs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Blog.Invitations;

/// <summary>
/// Blog invitations API for blog owners
/// </summary>
/// <remarks>
/// For accepting/rejecting invitations as recipient, use Personal API: /users/me/invitations
/// </remarks>
[ApiController]
[Route("v1/blogs/{id}/invitations")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Invitations")]
public class BlogInvitationController : ControllerBase
{
    private readonly IBlogInvitationApiService _apiService;
    private readonly IBlogApiService _blogApiService;

    /// <inheritdoc />
    public BlogInvitationController(
        IBlogInvitationApiService apiService,
        IBlogApiService blogApiService)
    {
        _apiService = apiService;
        _blogApiService = blogApiService;
    }

    private async Task<Guid> ResolveBlogId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : (await _blogApiService.GetByPublicId(id)).Resource.Id;

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="200">List of pending invitations</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to view this blog's invitations</response>
    /// <response code="404">Blog not found</response>
    [HttpGet(Name = nameof(GetBlogInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<BlogInvitation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogInvitations(string id)
    {
        var blogId = await ResolveBlogId(id);
        return Ok(await _apiService.GetBlogInvitations(blogId));
    }

    /// <summary>
    /// Invite an assistant to the blog
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Invitation request with username</param>
    /// <response code="201">Invitation created successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to invite to this blog</response>
    /// <response code="404">Blog or user not found</response>
    [HttpPost("assistants", Name = nameof(InviteBlogAssistant))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(BlogInvitation), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InviteBlogAssistant(string id, [FromBody] CreateInvitationRequest request)
    {
        var blogId = await ResolveBlogId(id);
        var result = await _apiService.CreateAssistantInvitation(blogId, request.Username);
        return CreatedAtRoute(nameof(GetBlogInvitations), new { id }, result);
    }

    /// <summary>
    /// Invite a reader to the blog
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Invitation request with username</param>
    /// <response code="201">Invitation created successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to invite to this blog</response>
    /// <response code="404">Blog or user not found</response>
    [HttpPost("readers", Name = nameof(InviteBlogReader))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(BlogInvitation), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InviteBlogReader(string id, [FromBody] CreateInvitationRequest request)
    {
        var blogId = await ResolveBlogId(id);
        var result = await _apiService.CreateReaderInvitation(blogId, request.Username);
        return CreatedAtRoute(nameof(GetBlogInvitations), new { id }, result);
    }

    /// <summary>
    /// Cancel a pending invitation
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="invitationId">Invitation identifier</param>
    /// <response code="204">Invitation cancelled</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to cancel this invitation</response>
    /// <response code="404">Invitation not found</response>
    [HttpDelete("{invitationId:guid}", Name = nameof(CancelBlogInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBlogInvitation(string id, Guid invitationId)
    {
        await _apiService.CancelInvitation(invitationId);
        return NoContent();
    }
}
