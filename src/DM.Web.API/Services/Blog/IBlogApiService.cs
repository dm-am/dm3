using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Dto.Blogs;
using DM.Web.API.Dto.Contracts;

namespace DM.Web.API.Services.Blog;

/// <summary>
/// API service for blog operations
/// </summary>
public interface IBlogApiService
{
    /// <summary>
    /// Get public blogs
    /// </summary>
    Task<ListEnvelope<Dto.Blogs.Blog>> GetPublicBlogs(PagingQuery query);

    /// <summary>
    /// Get popular blogs ordered by participant count
    /// </summary>
    Task<ListEnvelope<Dto.Blogs.Blog>> GetPopularBlogs();

    /// <summary>
    /// Get user blogs
    /// </summary>
    Task<ListEnvelope<Dto.Blogs.Blog>> GetUserBlogs(string login);

    /// <summary>
    /// Get blog by ID
    /// </summary>
    Task<Envelope<Dto.Blogs.Blog>> Get(Guid id);

    /// <summary>
    /// Get blog by owner login
    /// </summary>
    Task<Envelope<Dto.Blogs.Blog>> GetByOwnerLogin(string login);

    /// <summary>
    /// Create a new blog
    /// </summary>
    Task<Envelope<Dto.Blogs.Blog>> Create(CreateBlogRequest request);

    /// <summary>
    /// Update blog
    /// </summary>
    Task<Envelope<Dto.Blogs.Blog>> Update(Guid id, UpdateBlogRequest request);

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
