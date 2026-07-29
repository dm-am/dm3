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
    /// Count public blogs matching the filter. Takes the same filter object as
    /// <see cref="GetPublicBlogs" /> so the total cannot describe a different
    /// set than the page does.
    /// </summary>
    /// <param name="filter">Filter (host ids and current user already resolved by the service)</param>
    /// <param name="ct">Cancellation token</param>
    Task<int> CountPublicBlogs(BlogFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Get public blogs with paging
    /// </summary>
    /// <param name="paging">Paging data</param>
    /// <param name="filter">Filter and sort (host ids and current user already resolved by the service)</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Blog>> GetPublicBlogs(
        PagingData paging, BlogFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where user is owner or assistant
    /// </summary>
    Task<IEnumerable<Blog>> GetUserBlogs(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID
    /// </summary>
    Task<Blog?> Get(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by public ID (5-letter URL identifier)
    /// </summary>
    Task<Blog?> GetByPublicId(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner username (personal blog)
    /// </summary>
    Task<Blog?> GetByOwnerUsernameAsync(string username, CancellationToken ct = default);

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
    /// Get the user's most-liked publication across every blog they author.
    /// Published-only — drafts and unpublished posts are excluded so the
    /// profile widget never shows content the user hasn't released yet.
    /// Returns null when the user has no published publications at all.
    /// </summary>
    Task<Publication?> GetBestUserPublication(Guid authorId, CancellationToken ct = default);

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
    Task<IEnumerable<Blog>> GetPopularBlogs(
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
    Task<IEnumerable<Blog>> GetByIds(IEnumerable<Guid> blogIds, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where user is owner, mentor, or assistant
    /// </summary>
    Task<IEnumerable<Blog>> GetOwnBlogs(Guid userId, CancellationToken ct = default);

    // === WRITE ===

    /// <summary>
    /// Create a new blog
    /// </summary>
    /// <param name="entity">Blog creation entity</param>
    /// <param name="ct">Cancellation token</param>
    Task<Blog> CreateBlog(CreateBlogEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Update blog
    /// </summary>
    Task<Blog> UpdateBlog(UpdateBlogEntity entity, CancellationToken ct = default);

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
    /// Reorder the blog's rubrics: assign each rubric's sort order from its
    /// position in <paramref name="orderedRubricIds"/>. Ids not belonging to
    /// the blog are ignored.
    /// </summary>
    Task ReorderRubrics(Guid blogId, IReadOnlyList<Guid> orderedRubricIds, CancellationToken ct = default);

    /// <summary>
    /// Get the ids of published publications grouped by rubric for a blog.
    /// Used by the service to fill per-rubric unread counters (a rubric is
    /// not itself an unread-counter entity — its publications are).
    /// </summary>
    Task<IDictionary<Guid, Guid[]>> GetRubricPublicationIds(Guid blogId, CancellationToken ct = default);

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
