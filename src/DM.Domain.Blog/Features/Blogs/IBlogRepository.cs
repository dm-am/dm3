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
    /// <param name="ownerId">Whose blogs to list</param>
    /// <param name="viewerId">
    /// Who is reading, for <see cref="Blog.IsViewerSubscriber" />
    /// (<see cref="Guid.Empty" /> for a guest). Not the same person as
    /// <paramref name="ownerId" /> on any page but the user's own.
    /// </param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Blog>> GetUserBlogs(Guid ownerId, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="viewerId">
    /// The user the read is on behalf of (<see cref="Guid.Empty" /> for a guest);
    /// every authorization decision about a private draft is made on this read,
    /// and the reader role is <see cref="Blog.IsViewerSubscriber" />.
    /// </param>
    /// <param name="ct">Cancellation token</param>
    Task<Blog?> Get(Guid blogId, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by public ID (5-letter URL identifier)
    /// </summary>
    /// <param name="publicId">Readable blog address</param>
    /// <param name="viewerId">The user the read is on behalf of — see <see cref="Get" />.</param>
    /// <param name="ct">Cancellation token</param>
    Task<Blog?> GetByPublicId(string publicId, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner username (personal blog)
    /// </summary>
    /// <param name="username">Owner login</param>
    /// <param name="viewerId">The user the read is on behalf of — see <see cref="Get" />.</param>
    /// <param name="ct">Cancellation token</param>
    Task<Blog?> GetByOwnerUsernameAsync(string username, Guid viewerId, CancellationToken ct = default);

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
    /// <param name="viewerId">Who is reading, for <see cref="Blog.IsViewerSubscriber" /></param>
    /// <param name="excludeOwnerIds">Optional owner IDs to exclude (for blacklist filtering)</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Blog>> GetPopularBlogs(
        int count, Guid viewerId, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

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
    /// <param name="blogIds">Blogs to load</param>
    /// <param name="viewerId">Who is reading, for <see cref="Blog.IsViewerSubscriber" /></param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Blog>> GetByIds(IEnumerable<Guid> blogIds, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where user is owner, mentor, or assistant.
    /// The user is the reader here, so they are also who
    /// <see cref="Blog.IsViewerSubscriber" /> is filled for.
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
    /// Replace the order of the blog's rubrics: assign each rubric's sort order
    /// from its position in <paramref name="orderedRubricIds"/>. The blog bounds
    /// the write, so an id belonging to another blog is not written.
    /// </summary>
    /// <remarks>
    /// The caller passes the blog's rubrics in full, which is what the service
    /// enforces: a rubric this method does not write keeps a sort order the same
    /// call has just handed to another one.
    /// </remarks>
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
}
