using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Blog.Publications;

namespace DM.Web.API.Features.Blog.Blogs;

/// <summary>
/// API service for blog operations
/// </summary>
public interface IBlogApiService
{
    /// <summary>
    /// Get blogs with filtering and sorting (full, with rubrics array)
    /// </summary>
    Task<ListEnvelope<Blog>> GetBlogs(BlogsQuery query);

    /// <summary>
    /// Get blogs as lightweight refs (for sidebars/menus, no rubrics)
    /// </summary>
    Task<ListEnvelope<BlogRef>> GetBlogRefs(BlogsQuery query);

    /// <summary>
    /// Get blog by ID (lightweight)
    /// </summary>
    Task<Envelope<Blog>> Get(Guid id);

    /// <summary>
    /// Get blog by public ID (5 letters)
    /// </summary>
    Task<Envelope<Blog>> GetByPublicId(string publicId);

    /// <summary>
    /// Get blog by owner login (lightweight)
    /// </summary>
    Task<Envelope<Blog>> GetByOwnerLogin(string login);

    /// <summary>
    /// Resolve a route identifier of a blog, given either form.
    /// Every blog-scoped route accepts both, so without a shared resolver
    /// each controller carries its own copy of the branch.
    /// </summary>
    /// <param name="idOrPublicId">Blog public id (5 letters) or GUID</param>
    Task<Guid> ResolveId(string idOrPublicId);

    /// <summary>
    /// Create a new blog
    /// </summary>
    Task<Envelope<Blog>> Create(CreateBlogRequest request);

    /// <summary>
    /// Update blog
    /// </summary>
    Task<Envelope<Blog>> Update(Guid id, UpdateBlogRequest request);

    /// <summary>
    /// Delete blog
    /// </summary>
    Task Delete(Guid id);

    /// <summary>
    /// Apply a premoderation transition (send to / remove from premoderation)
    /// </summary>
    /// <param name="id">Blog public id (5 letters) or GUID</param>
    /// <param name="request">Requested transition</param>
    /// <returns>Envelope for updated blog</returns>
    Task<Envelope<Blog>> ChangePremoderation(string id, BlogPremoderationChangeRequest request);

    /// <summary>
    /// Apply a blog status transition (start / freeze / finish / close / reopen)
    /// </summary>
    /// <param name="id">Blog public id (5 letters) or GUID</param>
    /// <param name="request">Requested transition</param>
    /// <returns>Envelope for updated blog</returns>
    Task<Envelope<Blog>> ChangeStatus(string id, BlogStatusChangeRequest request);

    /// <summary>
    /// Create a new rubric
    /// </summary>
    Task<Envelope<Rubric>> CreateRubric(Guid blogId, CreateRubricRequest request);

    /// <summary>
    /// Rename a rubric
    /// </summary>
    Task<Envelope<Rubric>> UpdateRubric(Guid rubricId, UpdateRubricRequest request);

    /// <summary>
    /// Reorder a blog's rubrics (ordered rubric ids)
    /// </summary>
    Task<ListEnvelope<Rubric>> ReorderRubrics(Guid blogId, ReorderRubricsRequest request);

    /// <summary>
    /// Delete rubric
    /// </summary>
    Task DeleteRubric(Guid rubricId);

    /// <summary>
    /// Get publications for a blog
    /// </summary>
    Task<ListEnvelope<Publication>> GetPublications(Guid blogId, Guid? rubricId, PagingQuery query);

    /// <summary>
    /// Get publication by ID
    /// </summary>
    Task<Envelope<Publication>> GetPublication(Guid publicationId);

    /// <summary>
    /// Get the user's most-liked (published) publication across all blogs.
    /// Returns null inside the envelope when the user has no published
    /// publications — the API layer turns that into a 404 so callers can
    /// branch on response status without parsing the body.
    /// </summary>
    Task<Envelope<Publication?>> GetUserBestPublication(string username);

    /// <summary>
    /// Create a new publication
    /// </summary>
    Task<Envelope<Publication>> CreatePublication(Guid blogId, CreatePublicationRequest request);

    /// <summary>
    /// Update publication
    /// </summary>
    Task<Envelope<Publication>> UpdatePublication(Guid publicationId, UpdatePublicationRequest request);

    /// <summary>
    /// Delete publication
    /// </summary>
    Task DeletePublication(Guid publicationId);
}
