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
    /// Get list of blogs
    /// </summary>
    /// <param name="q">Query parameters for filtering and sorting</param>
    /// <remarks>
    /// Query examples:
    /// - Popular blogs: `?sortBy=popularity&amp;take=10`
    /// - My blogs: `?participating=true`
    /// - User's blogs (owner or assistant): `?authorUsername=john`
    ///
    /// Use `projection=ref` for lightweight sidebar/menu data (no rubrics).
    /// Default projection returns full Blog with rubrics array.
    /// </remarks>
    /// <response code="200">List of blogs</response>
    [HttpGet(Name = nameof(GetBlogs))]
    [ProducesResponseType(typeof(ListEnvelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ListEnvelope<BlogRef>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlogs([FromQuery] BlogsQuery q)
    {
        // Response contains user-specific unread counts, cannot use public cache
        Response.Headers.CacheControl = "private, no-store";

        // Return lightweight refs for sidebars, full blogs for detail views
        if (string.Equals(q.Projection, "ref", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(await _apiService.GetBlogRefs(q));
        }

        return Ok(await _apiService.GetBlogs(q));
    }

    /// <summary>
    /// Get blog by ID
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="200">Blog details</response>
    /// <response code="404">Blog not found</response>
    [HttpGet("{id}", Name = nameof(GetBlog))]
    [ProducesResponseType(typeof(Envelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlog(string id)
    {
        if (Guid.TryParse(id, out var guid))
            return Ok(await _apiService.Get(guid));
        return Ok(await _apiService.GetByPublicId(id));
    }

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
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Blog update request</param>
    /// <response code="200">Blog updated successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to update this blog</response>
    /// <response code="404">Blog not found</response>
    [HttpPatch("{id}", Name = nameof(PatchBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchBlog(string id, [FromBody] UpdateBlogRequest request)
    {
        var blogId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _apiService.GetByPublicId(id)).Resource.Id;
        return Ok(await _apiService.Update(blogId, request));
    }

    /// <summary>
    /// Delete blog (soft delete)
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <response code="204">Blog deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to delete this blog</response>
    /// <response code="404">Blog not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteBlog))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBlog(string id)
    {
        var blogId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _apiService.GetByPublicId(id)).Resource.Id;
        await _apiService.Delete(blogId);
        return NoContent();
    }

    /// <summary>
    /// Create a new rubric in blog
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Rubric creation request</param>
    /// <response code="201">Rubric created successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Blog not found</response>
    [HttpPost("{id}/rubrics", Name = nameof(PostRubric))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Rubric>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostRubric(string id, [FromBody] CreateRubricRequest request)
    {
        var blogId = Guid.TryParse(id, out var guid)
            ? guid
            : (await _apiService.GetByPublicId(id)).Resource.Id;
        var result = await _apiService.CreateRubric(blogId, request);
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
