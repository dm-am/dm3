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
    /// Get blog details by ID (full, with subscribers and assistants)
    /// </summary>
    Task<Envelope<BlogDetails>> GetDetails(Guid id);

    /// <summary>
    /// Get blog details by public ID (5 letters)
    /// </summary>
    Task<Envelope<BlogDetails>> GetDetailsByPublicId(string publicId);

    /// <summary>
    /// Get blog by owner login (lightweight)
    /// </summary>
    Task<Envelope<Blog>> GetByOwnerLogin(string login);

    /// <summary>
    /// Get blog details by owner login (full)
    /// </summary>
    Task<Envelope<BlogDetails>> GetDetailsByOwnerLogin(string login);

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
    /// Create a new rubric
    /// </summary>
    Task<Envelope<Rubric>> CreateRubric(Guid blogId, CreateRubricRequest request);

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
