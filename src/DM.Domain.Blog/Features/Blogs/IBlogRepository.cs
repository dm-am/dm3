using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Unified blog repository
/// </summary>
public interface IBlogRepository
{
    // === READ ===

    /// <summary>
    /// Count public blogs
    /// </summary>
    /// <param name="excludeOwnerIds">Optional owner IDs to exclude (for blacklist filtering)</param>
    /// <param name="ct">Cancellation token</param>
    Task<int> CountPublicBlogs(IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get public blogs with paging
    /// </summary>
    /// <param name="paging">Paging data</param>
    /// <param name="excludeOwnerIds">Optional owner IDs to exclude (for blacklist filtering)</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<BlogModel>> GetPublicBlogs(
        PagingData paging, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get blogs by owner
    /// </summary>
    Task<IEnumerable<BlogModel>> GetUserBlogs(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID
    /// </summary>
    Task<BlogModel?> Get(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner username (personal blog)
    /// </summary>
    Task<BlogModel?> GetByOwnerUsername(string username, CancellationToken ct = default);

    /// <summary>
    /// Count publications for a blog
    /// </summary>
    Task<int> CountPublications(Guid blogId, Guid? rubricId, bool includeUnpublished, CancellationToken ct = default);

    /// <summary>
    /// Get publications for a blog
    /// </summary>
    Task<IEnumerable<Publication>> GetPublications(
        Guid blogId, Guid? rubricId, bool includeUnpublished, PagingData paging, CancellationToken ct = default);

    /// <summary>
    /// Get publication by ID
    /// </summary>
    Task<Publication?> GetPublication(Guid publicationId, CancellationToken ct = default);

    /// <summary>
    /// Get rubrics for a blog
    /// </summary>
    Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get rubric by ID with its blog ID
    /// </summary>
    Task<(Rubric? rubric, Guid blogId)> GetRubric(Guid rubricId, CancellationToken ct = default);

    /// <summary>
    /// Get popular blogs ordered by subscriber count
    /// </summary>
    /// <param name="count">Number of blogs to return</param>
    /// <param name="excludeOwnerIds">Optional owner IDs to exclude (for blacklist filtering)</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<BlogModel>> GetPopularBlogs(
        int count, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get blog readers (subscribers from Subscriptions table)
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetReaders(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog assistants (from BlogAssistants table)
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetAssistants(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog assistants with JoinedUtc (from BlogAssistants table)
    /// </summary>
    Task<IEnumerable<BlogUser>> GetAssistantsWithJoinDate(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blogs by IDs
    /// </summary>
    Task<IEnumerable<BlogModel>> GetByIds(IEnumerable<Guid> blogIds, CancellationToken ct = default);

    // === WRITE ===

    /// <summary>
    /// Create a new blog
    /// </summary>
    /// <param name="entity">Blog creation entity</param>
    /// <param name="ct">Cancellation token</param>
    Task<BlogModel> CreateBlog(CreateBlogEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Update blog
    /// </summary>
    Task<BlogModel> UpdateBlog(UpdateBlogEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Delete blog (soft delete)
    /// </summary>
    Task DeleteBlog(Guid blogId, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Create a new rubric
    /// </summary>
    Task<Rubric> CreateRubric(CreateRubricEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Update rubric
    /// </summary>
    Task<Rubric> UpdateRubric(UpdateRubricEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Delete rubric (soft delete)
    /// </summary>
    Task DeleteRubric(Guid rubricId, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Create a new publication
    /// </summary>
    /// <param name="entity">Publication creation entity</param>
    /// <param name="ct">Cancellation token</param>
    Task<Publication> CreatePublication(CreatePublicationEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Update publication
    /// </summary>
    Task<Publication> UpdatePublication(UpdatePublicationEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Delete publication (soft delete)
    /// </summary>
    Task DeletePublication(Guid publicationId, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Add an assistant to a blog
    /// </summary>
    Task AddAssistant(AddBlogAssistantEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Check if user is an assistant
    /// </summary>
    Task<bool> IsAssistant(Guid blogId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Remove an assistant from a blog
    /// </summary>
    Task<bool> RemoveAssistant(Guid blogId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Remove an assistant from a blog by username
    /// </summary>
    Task<bool> RemoveAssistantByUsername(Guid blogId, string username, CancellationToken ct = default);

    /// <summary>
    /// Check if user is a subscriber (reader)
    /// </summary>
    Task<bool> IsSubscriber(Guid blogId, Guid userId, CancellationToken ct = default);
}
