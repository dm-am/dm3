using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Blog.Blogs;
using DM.Web.API.Features.Blog.Likes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Blog.Publications;

/// <summary>
/// API controller for managing blog publications
/// </summary>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Publications")]
public class PublicationController : ControllerBase
{
    private readonly IBlogApiService _apiService;
    private readonly IBlogLikeApiService _likeApiService;

    /// <inheritdoc />
    public PublicationController(
        IBlogApiService apiService,
        IBlogLikeApiService likeApiService)
    {
        _apiService = apiService;
        _likeApiService = likeApiService;
    }

    /// <summary>
    /// Get publications for a blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="rubricId">Filter by rubric (optional)</param>
    /// <param name="q">Paging query parameters</param>
    /// <response code="200">List of publications</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("blogs/{blogId:guid}/publications", Name = nameof(GetPublications))]
    [ProducesResponseType(typeof(ListEnvelope<Publication>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublications(Guid blogId, [FromQuery] Guid? rubricId, [FromQuery] PagingQuery q) =>
        Ok(await _apiService.GetPublications(blogId, rubricId, q));

    /// <summary>
    /// Get publication by ID
    /// </summary>
    /// <param name="id">Publication identifier</param>
    /// <response code="200">Publication details</response>
    /// <response code="404">Publication not found</response>
    [HttpGet("publications/{id:guid}", Name = nameof(GetPublication))]
    [ProducesResponseType(typeof(Envelope<Publication>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublication(Guid id) =>
        Ok(await _apiService.GetPublication(id));

    /// <summary>
    /// Create a new publication
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="request">Publication creation request</param>
    /// <response code="201">Publication created successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Blog not found</response>
    [HttpPost("blogs/{blogId:guid}/publications", Name = nameof(PostPublication))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Publication>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostPublication(Guid blogId, [FromBody] CreatePublicationRequest request)
    {
        var result = await _apiService.CreatePublication(blogId, request);
        return CreatedAtRoute(nameof(GetPublication), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Update publication
    /// </summary>
    /// <param name="id">Publication identifier</param>
    /// <param name="request">Publication update request</param>
    /// <response code="200">Publication updated successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Publication not found</response>
    [HttpPatch("publications/{id:guid}", Name = nameof(PatchPublication))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Publication>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchPublication(Guid id, [FromBody] UpdatePublicationRequest request) =>
        Ok(await _apiService.UpdatePublication(id, request));

    /// <summary>
    /// Delete publication (soft delete)
    /// </summary>
    /// <param name="id">Publication identifier</param>
    /// <response code="204">Publication deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Publication not found</response>
    [HttpDelete("publications/{id:guid}", Name = nameof(DeletePublication))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePublication(Guid id)
    {
        await _apiService.DeletePublication(id);
        return NoContent();
    }

    /// <summary>
    /// Like a publication
    /// </summary>
    /// <remarks>
    /// Adds a like to the publication from the current user.
    /// Users cannot like their own publications.
    /// Each user can only like a publication once.
    ///
    /// Example request:
    ///     POST /v1/publications/3fa85f64-5717-4562-b3fc-2c963f66afa6/likes
    /// </remarks>
    /// <param name="id">Publication identifier (GUID)</param>
    /// <response code="201">Like added, returns user who liked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot like this publication (e.g., own publication)</response>
    /// <response code="404">Publication not found</response>
    /// <response code="409">User already liked this publication</response>
    [HttpPost("publications/{id:guid}/likes", Name = nameof(PostPublicationLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostPublicationLike(Guid id)
    {
        var result = await _likeApiService.LikePublication(id);
        return CreatedAtRoute(nameof(GetPublication), new { id }, result);
    }

    /// <summary>
    /// Remove like from publication
    /// </summary>
    /// <remarks>
    /// Removes the current user's like from the publication.
    /// Can only remove your own likes.
    ///
    /// Example request:
    ///     DELETE /v1/publications/3fa85f64-5717-4562-b3fc-2c963f66afa6/likes
    /// </remarks>
    /// <param name="id">Publication identifier (GUID)</param>
    /// <response code="204">Like removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot remove like (not their like)</response>
    /// <response code="404">Publication not found</response>
    /// <response code="409">User has not liked this publication</response>
    [HttpDelete("publications/{id:guid}/likes", Name = nameof(DeletePublicationLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePublicationLike(Guid id)
    {
        await _likeApiService.UnlikePublication(id);
        return NoContent();
    }
}
