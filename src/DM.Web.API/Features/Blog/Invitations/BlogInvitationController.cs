using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
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
[Route("v1/blogs/{id:guid}/invitations")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Invitations")]
public class BlogInvitationController : ControllerBase
{
    private readonly IBlogInvitationApiService _apiService;

    /// <inheritdoc />
    public BlogInvitationController(IBlogInvitationApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <response code="200">List of pending invitations</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to view this blog's invitations</response>
    /// <response code="404">Blog not found</response>
    [HttpGet(Name = nameof(GetBlogInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<BlogInvitation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogInvitations(Guid id) =>
        Ok(await _apiService.GetBlogInvitations(id));

    /// <summary>
    /// Invite an assistant to the blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="request">Invitation request with username</param>
    /// <response code="201">Invitation created successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to invite to this blog</response>
    /// <response code="404">Blog or user not found</response>
    [HttpPost("assistants", Name = nameof(InviteBlogAssistant))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(BlogInvitation), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InviteBlogAssistant(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var result = await _apiService.CreateAssistantInvitation(id, request.Username);
        return CreatedAtRoute(nameof(GetBlogInvitations), new { id }, result);
    }

    /// <summary>
    /// Invite a reader to the blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="request">Invitation request with username</param>
    /// <response code="201">Invitation created successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to invite to this blog</response>
    /// <response code="404">Blog or user not found</response>
    [HttpPost("readers", Name = nameof(InviteBlogReader))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(BlogInvitation), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InviteBlogReader(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var result = await _apiService.CreateReaderInvitation(id, request.Username);
        return CreatedAtRoute(nameof(GetBlogInvitations), new { id }, result);
    }

    /// <summary>
    /// Cancel a pending invitation
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="invitationId">Invitation identifier</param>
    /// <response code="204">Invitation cancelled</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to cancel this invitation</response>
    /// <response code="404">Invitation not found</response>
    [HttpDelete("{invitationId:guid}", Name = nameof(CancelBlogInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBlogInvitation(Guid id, Guid invitationId)
    {
        await _apiService.CancelInvitation(invitationId);
        return NoContent();
    }
}
