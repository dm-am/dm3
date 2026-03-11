using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Blog.Blogs;

/// <summary>
/// API controller for managing user blogs
/// </summary>
[ApiController]
[Route("v1/blogs")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Blogs")]
public class BlogController : ControllerBase
{
    private readonly IBlogApiService _apiService;

    /// <inheritdoc />
    public BlogController(IBlogApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get public blogs
    /// </summary>
    /// <param name="q">Paging query parameters</param>
    /// <response code="200">List of public blogs</response>
    [HttpGet(Name = nameof(GetBlogs))]
    [ProducesResponseType(typeof(ListEnvelope<Blog>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlogs([FromQuery] PagingQuery q) =>
        Ok(await _apiService.GetPublicBlogs(q));

    /// <summary>
    /// Get popular blogs ordered by participant count
    /// </summary>
    /// <response code="200">List of popular blogs</response>
    [HttpGet("popular", Name = nameof(GetPopularBlogs))]
    [ProducesResponseType(typeof(ListEnvelope<Blog>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPopularBlogs() =>
        Ok(await _apiService.GetPopularBlogs());

    /// <summary>
    /// Get blogs by user login
    /// </summary>
    /// <param name="login">User login</param>
    /// <response code="200">List of user blogs</response>
    /// <response code="404">User not found</response>
    [HttpGet("user/{login}", Name = nameof(GetUserBlogs))]
    [ProducesResponseType(typeof(ListEnvelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserBlogs(string login) =>
        Ok(await _apiService.GetUserBlogs(login));

    /// <summary>
    /// Get blog by ID
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <response code="200">Blog details</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetBlog))]
    [ProducesResponseType(typeof(Envelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlog(Guid id) =>
        Ok(await _apiService.Get(id));

    /// <summary>
    /// Get blog by owner login (personal blog)
    /// </summary>
    /// <param name="login">Owner login</param>
    /// <response code="200">Blog details</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("owner/{login}", Name = nameof(GetBlogByOwner))]
    [ProducesResponseType(typeof(Envelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogByOwner(string login) =>
        Ok(await _apiService.GetByOwnerLogin(login));

    /// <summary>
    /// Create a new blog
    /// </summary>
    /// <param name="request">Blog creation request</param>
    /// <response code="201">Blog created successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost(Name = nameof(PostBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Blog>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PostBlog([FromBody] CreateBlogRequest request)
    {
        var result = await _apiService.Create(request);
        return CreatedAtRoute(nameof(GetBlog), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Update blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="request">Blog update request</param>
    /// <response code="200">Blog updated successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to update this blog</response>
    /// <response code="404">Blog not found</response>
    [HttpPatch("{id:guid}", Name = nameof(PatchBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchBlog(Guid id, [FromBody] UpdateBlogRequest request) =>
        Ok(await _apiService.Update(id, request));

    /// <summary>
    /// Delete blog (soft delete)
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <response code="204">Blog deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to delete this blog</response>
    /// <response code="404">Blog not found</response>
    [HttpDelete("{id:guid}", Name = nameof(DeleteBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBlog(Guid id)
    {
        await _apiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Create a new rubric in blog
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="request">Rubric creation request</param>
    /// <response code="201">Rubric created successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Blog not found</response>
    [HttpPost("{id:guid}/rubrics", Name = nameof(PostRubric))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Rubric>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostRubric(Guid id, [FromBody] CreateRubricRequest request)
    {
        var result = await _apiService.CreateRubric(id, request);
        return Created("", result);
    }

    /// <summary>
    /// Delete rubric
    /// </summary>
    /// <param name="rubricId">Rubric identifier</param>
    /// <response code="204">Rubric deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Rubric not found</response>
    [HttpDelete("rubrics/{rubricId:guid}", Name = nameof(DeleteRubric))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRubric(Guid rubricId)
    {
        await _apiService.DeleteRubric(rubricId);
        return NoContent();
    }
}
