using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Blogs.Reading;
using DM.Services.Community.BusinessProcesses.Blogs.Writing;
using DM.Services.Core.Dto;
using DbBlogParticipation = DM.Services.DataAccess.BusinessObjects.Blogs.BlogParticipation;

namespace DM.Services.Community.BusinessProcesses.Blogs;

/// <summary>
/// Service for blog operations
/// </summary>
public interface IBlogService
{
    /// <summary>
    /// Get public blogs with paging
    /// </summary>
    Task<(IEnumerable<Blog> blogs, PagingResult paging)> GetPublicBlogs(PagingQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get popular blogs ordered by participant count
    /// </summary>
    Task<IEnumerable<Blog>> GetPopularBlogs(int count = 5, CancellationToken ct = default);

    /// <summary>
    /// Get blogs by user login
    /// </summary>
    Task<IEnumerable<Blog>> GetUserBlogs(string login, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID
    /// </summary>
    Task<Blog> Get(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID (for authorization checks)
    /// </summary>
    Task<Blog> GetBlog(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner login (personal blog)
    /// </summary>
    Task<Blog> GetByOwnerLogin(string login, CancellationToken ct = default);

    /// <summary>
    /// Add a participant to a blog
    /// </summary>
    Task AddParticipant(Guid blogId, Guid userId, DbBlogParticipation role, CancellationToken ct = default);

    /// <summary>
    /// Create a new blog
    /// </summary>
    Task<Blog> Create(CreateBlog createBlog, CancellationToken ct = default);

    /// <summary>
    /// Update blog
    /// </summary>
    Task<Blog> Update(UpdateBlog updateBlog, CancellationToken ct = default);

    /// <summary>
    /// Delete blog
    /// </summary>
    Task Delete(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get publications for a blog
    /// </summary>
    Task<(IEnumerable<Publication> publications, PagingResult paging)> GetPublications(
        Guid blogId, Guid? rubricId, PagingQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get publication by ID
    /// </summary>
    Task<Publication> GetPublication(Guid publicationId, CancellationToken ct = default);

    /// <summary>
    /// Create a new publication
    /// </summary>
    Task<Publication> CreatePublication(CreatePublication createPublication, CancellationToken ct = default);

    /// <summary>
    /// Update publication
    /// </summary>
    Task<Publication> UpdatePublication(UpdatePublication updatePublication, CancellationToken ct = default);

    /// <summary>
    /// Delete publication
    /// </summary>
    Task DeletePublication(Guid publicationId, CancellationToken ct = default);

    /// <summary>
    /// Create a new rubric
    /// </summary>
    Task<Rubric> CreateRubric(CreateRubric createRubric, CancellationToken ct = default);

    /// <summary>
    /// Delete rubric
    /// </summary>
    Task DeleteRubric(Guid rubricId, CancellationToken ct = default);
}
