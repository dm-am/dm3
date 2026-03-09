using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Service for blog operations
/// </summary>
public interface IBlogService
{
    /// <summary>
    /// Get public blogs with paging
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="excludeOwnerIds">Optional owner IDs to exclude (for blacklist filtering)</param>
    /// <param name="ct">Cancellation token</param>
    Task<(IEnumerable<BlogModel> blogs, PagingResult paging)> GetPublicBlogs(
        PagingQuery query, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get popular blogs ordered by subscriber count
    /// </summary>
    /// <param name="count">Number of blogs to return</param>
    /// <param name="excludeOwnerIds">Optional owner IDs to exclude (for blacklist filtering)</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<BlogModel>> GetPopularBlogs(
        int count = 5, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get blogs by username
    /// </summary>
    Task<IEnumerable<BlogModel>> GetUserBlogs(string username, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID
    /// </summary>
    Task<BlogModel> Get(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID (for authorization checks)
    /// </summary>
    Task<BlogModel> GetBlog(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner username (personal blog)
    /// </summary>
    Task<BlogModel> GetByOwnerUsername(string username, CancellationToken ct = default);

    /// <summary>
    /// Add an assistant to a blog
    /// </summary>
    Task AddAssistant(Guid blogId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Create a new blog
    /// </summary>
    Task<BlogModel> Create(CreateBlog createBlog, CancellationToken ct = default);

    /// <summary>
    /// Update blog
    /// </summary>
    Task<BlogModel> Update(UpdateBlog updateBlog, CancellationToken ct = default);

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

    /// <summary>
    /// Get rubrics for a blog
    /// </summary>
    Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to a blog as a reader (via Subscriptions)
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The subscribed user</returns>
    Task<GeneralUser> Subscribe(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribe from a blog (via Subscriptions)
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task Unsubscribe(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Leave a blog (remove current user as assistant)
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task Leave(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog readers (subscribers)
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetReaders(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog assistants with role information
    /// </summary>
    Task<IEnumerable<BlogUser>> GetAssistants(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Remove assistant from blog
    /// </summary>
    Task RemoveAssistant(Guid blogId, string username, CancellationToken ct = default);

    /// <summary>
    /// Get blogs by IDs (for subscribed blogs)
    /// </summary>
    Task<IEnumerable<BlogModel>> GetSubscribedBlogs(IEnumerable<Guid> blogIds, CancellationToken ct = default);
}
