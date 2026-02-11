using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Blogs;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Blog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Blog;

/// <summary>
/// API controller for blog invitations
/// </summary>
[ApiController]
[Route("v1/blogs")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Blog Invitations")]
public class BlogInvitationsController : ControllerBase
{
    private readonly IBlogInvitationApiService _apiService;

    /// <inheritdoc />
    public BlogInvitationsController(IBlogInvitationApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Create an assistant invitation for a blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="request">Invitation request</param>
    /// <response code="201">Invitation created successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to invite to this blog</response>
    /// <response code="404">Blog or user not found</response>
    [HttpPost("{id:guid}/invitations/assistant", Name = nameof(CreateAssistantInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<BlogInvitation>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> CreateAssistantInvitation(
        Guid id,
        [FromBody] CreateAssistantInvitationRequest request)
    {
        var result = await _apiService.CreateAssistantInvitation(id, request.Login);
        return Created("", result);
    }

    /// <summary>
    /// Create a reader invitation for a blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="request">Invitation request</param>
    /// <response code="201">Invitation created successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to invite to this blog</response>
    /// <response code="404">Blog or user not found</response>
    [HttpPost("{id:guid}/invitations/reader", Name = nameof(CreateReaderInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<BlogInvitation>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> CreateReaderInvitation(
        Guid id,
        [FromBody] CreateReaderInvitationRequest request)
    {
        var result = await _apiService.CreateReaderInvitation(id, request.Login);
        return Created("", result);
    }

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <response code="200">List of pending invitations</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to view this blog's invitations</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("{id:guid}/invitations", Name = nameof(GetBlogInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<BlogInvitation>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetBlogInvitations(Guid id) =>
        Ok(await _apiService.GetBlogInvitations(id));

    /// <summary>
    /// Get pending invitations for current user
    /// </summary>
    /// <response code="200">List of pending invitations</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("invitations/my", Name = nameof(GetMyBlogInvitations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<BlogInvitation>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetMyBlogInvitations() =>
        Ok(await _apiService.GetMyInvitations());

    /// <summary>
    /// Accept a blog invitation
    /// </summary>
    /// <param name="tokenId">Invitation token identifier</param>
    /// <response code="204">Invitation accepted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Invitation not found or expired</response>
    [HttpPost("invitations/{tokenId:guid}/accept", Name = nameof(AcceptBlogInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> AcceptBlogInvitation(Guid tokenId)
    {
        await _apiService.AcceptInvitation(tokenId);
        return NoContent();
    }

    /// <summary>
    /// Reject a blog invitation
    /// </summary>
    /// <param name="tokenId">Invitation token identifier</param>
    /// <response code="204">Invitation rejected</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Invitation not found or expired</response>
    [HttpPost("invitations/{tokenId:guid}/reject", Name = nameof(RejectBlogInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> RejectBlogInvitation(Guid tokenId)
    {
        await _apiService.RejectInvitation(tokenId);
        return NoContent();
    }

    /// <summary>
    /// Cancel a pending invitation (by blog owner)
    /// </summary>
    /// <param name="tokenId">Invitation token identifier</param>
    /// <response code="204">Invitation cancelled</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to cancel this invitation</response>
    /// <response code="404">Invitation not found</response>
    [HttpDelete("invitations/{tokenId:guid}", Name = nameof(CancelBlogInvitation))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> CancelBlogInvitation(Guid tokenId)
    {
        await _apiService.CancelInvitation(tokenId);
        return NoContent();
    }
}
