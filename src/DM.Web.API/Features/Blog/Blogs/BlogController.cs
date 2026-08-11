using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Swagger;
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
    /// <response code="200">List of blogs. With `projection=ref` the items are
    /// `BlogRef` (no rubrics) rather than `Blog`; one operation cannot declare
    /// two schemas for one status, so only the default projection is described
    /// below — and it is the wider of the two, since Blog derives from BlogRef.</response>
    [HttpGet(Name = nameof(GetBlogs))]
    [ProducesResponseType(typeof(ListEnvelope<Blog>), StatusCodes.Status200OK)]
    // Response carries per-caller unread counts, so it must not be cached
    // anywhere. Declared, not assigned by hand: one mechanism for cache policy.
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetBlogs([FromQuery] BlogsQuery q)
    {
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchBlog(string id, [FromBody] UpdateBlogRequest request)
    {
        var blogId = await _apiService.ResolveId(id);
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBlog(string id)
    {
        var blogId = await _apiService.ResolveId(id);
        await _apiService.Delete(blogId);
        return NoContent();
    }

    /// <summary>
    /// Change blog status
    /// </summary>
    /// <remarks>
    /// Applies a single status transition on the blog state machine:
    /// `Start` (Draft-&gt;Active), `Freeze` (Active-&gt;Closed/Frozen),
    /// `Finish` (Active-&gt;Closed/Finished), `Close` (Active or Frozen-&gt;Closed),
    /// `Reopen` (Closed-&gt;Active). Only the blog leads (owner and assistants)
    /// may change the status. Illegal transitions are rejected with 400.
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Requested status transition</param>
    /// <response code="200">Returns the updated blog</response>
    /// <response code="400">The requested transition is illegal for the current status</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change the status of this blog</response>
    /// <response code="404">Blog not found</response>
    [HttpPost("{id}/status", Name = nameof(PostBlogStatus))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostBlogStatus(string id, [FromBody] BlogStatusChangeRequest request)
    {
        // Pass the raw id through: the domain resolves the public id via the
        // repository (ungated) after the authentication gate. Resolving here
        // through the read-gated GetByPublicId would hide a premoderation-
        // pending blog from its own leads on this lead-only endpoint.
        return Ok(await _apiService.ChangeStatus(id, request));
    }

    /// <summary>
    /// Change blog premoderation state
    /// </summary>
    /// <remarks>
    /// Mentor action: `SendToPremoderation` (AwaitingEdits-&gt;AwaitingApproval)
    /// puts the blog back in the review queue and assigns the acting mentor as
    /// curator; `RemoveFromPremoderation` (AwaitingApproval-&gt;Approved) releases
    /// the blog so it becomes publicly visible. Requires Mentor role or above.
    /// Illegal transitions are rejected with 400.
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Requested premoderation transition</param>
    /// <response code="200">Returns the updated blog</response>
    /// <response code="400">The requested transition is illegal for the current premoderation state</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not a mentor</response>
    /// <response code="404">Blog not found</response>
    [HttpPost("{id}/premoderation", Name = nameof(PostBlogPremoderation))]
    [RequireRole(UserRole.Mentor)]
    [ProducesResponseType(typeof(Envelope<Blog>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostBlogPremoderation(string id, [FromBody] BlogPremoderationChangeRequest request)
    {
        // Pass the raw id through: the domain resolves the public id via the
        // repository (ungated) after the Mentor gate. Resolving here through the
        // read-gated GetByPublicId would hide a premoderation-pending blog from
        // the non-curator mentor this endpoint exists for.
        return Ok(await _apiService.ChangePremoderation(id, request));
    }

    /// <summary>
    /// Create a new rubric in blog
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Rubric creation request</param>
    /// <response code="201">Rubric created, returns the rubric</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Blog not found</response>
    [HttpPost("{id}/rubrics", Name = nameof(PostRubric))]
    [AuthenticationRequired]
    // 201 without Location. A rubric has no address of its own, and
    // API_DESIGN.md answers exactly that case: the header is left off rather
    // than pointed at the collection, which a consumer resolving it against
    // the request would read as the address of the created record. The status
    // still says what happened, and dropping it to 200 said "here is a
    // representation" about a request that created something.
    [CreatedWithoutLocation]
    [ProducesResponseType(typeof(Envelope<Rubric>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostRubric(string id, [FromBody] CreateRubricRequest request)
    {
        var blogId = await _apiService.ResolveId(id);
        return StatusCode(StatusCodes.Status201Created, await _apiService.CreateRubric(blogId, request));
    }

    /// <summary>
    /// Rename a rubric
    /// </summary>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="rubricId">Rubric identifier</param>
    /// <param name="request">Rubric rename request</param>
    /// <response code="200">Rubric renamed successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Rubric not found</response>
    [HttpPatch("{id}/rubrics/{rubricId:guid}", Name = nameof(PatchRubric))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Rubric>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchRubric(string id, Guid rubricId, [FromBody] UpdateRubricRequest request)
    {
        // The rubric id alone identifies the rubric; the blog {id} is kept in
        // the route for a consistent nested contract. The domain resolves and
        // authorizes via the rubric's own blog.
        return Ok(await _apiService.UpdateRubric(rubricId, request));
    }

    /// <summary>
    /// Reorder a blog's rubrics
    /// </summary>
    /// <remarks>
    /// Accepts the rubric identifiers in the desired order; each rubric's sort
    /// order becomes its position in the list. Only the blog owner may reorder.
    /// </remarks>
    /// <param name="id">Blog public ID (5 letters) or GUID</param>
    /// <param name="request">Ordered rubric identifiers</param>
    /// <response code="200">Rubrics reordered; returns the rubrics in their new order</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized</response>
    /// <response code="404">Blog not found</response>
    [HttpPut("{id}/rubrics/order", Name = nameof(PutRubricsOrder))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Rubric>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutRubricsOrder(string id, [FromBody] ReorderRubricsRequest request)
    {
        var blogId = await _apiService.ResolveId(id);
        return Ok(await _apiService.ReorderRubrics(blogId, request));
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRubric(Guid rubricId)
    {
        await _apiService.DeleteRubric(rubricId);
        return NoContent();
    }
}
