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
    /// Get public blogs with paging. Resolves the filter's host usernames into
    /// user ids and role-gates its premoderation status before querying, so
    /// callers pass what the request said and nothing more.
    /// </summary>
    /// <param name="query">Paging query</param>
    /// <param name="filter">Filter and sort (an empty filter means the whole public list)</param>
    /// <param name="ct">Cancellation token</param>
    Task<(IEnumerable<Blog> blogs, PagingResult paging)> GetPublicBlogs(
        PagingQuery query, BlogFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Get popular blogs ordered by subscriber count
    /// </summary>
    /// <param name="count">Number of blogs to return</param>
    /// <param name="excludeOwnerIds">Optional owner IDs to exclude (for blacklist filtering)</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Blog>> GetPopularBlogs(
        int count = 5, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where user is owner or assistant
    /// </summary>
    Task<IEnumerable<Blog>> GetUserBlogs(string username, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID
    /// </summary>
    Task<Blog> GetAsync(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by public ID (5-letter URL identifier)
    /// </summary>
    Task<Blog> GetByPublicIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID (for authorization checks, skips draft visibility)
    /// </summary>
    Task<Blog> GetBlogAsync(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner username (personal blog)
    /// </summary>
    Task<Blog> GetByOwnerUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Add an assistant to a blog
    /// </summary>
    Task AddAssistant(Guid blogId, Guid userId, CancellationToken ct = default);

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
    /// Get a user's best (most-liked, published-only) publication across
    /// every blog they author. Used by the profile "Blogs" tab. Returns
    /// null when the user has no published publications.
    /// </summary>
    Task<Publication?> GetBestUserPublication(string username, CancellationToken ct = default);

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
    /// Rename a rubric (and optionally change its sort order). Gated to the
    /// blog owner, mirroring how rubrics are created and deleted.
    /// </summary>
    Task<Rubric> UpdateRubric(UpdateRubric updateRubric, CancellationToken ct = default);

    /// <summary>
    /// Reorder the rubrics of a blog. The position of each id in
    /// <paramref name="orderedRubricIds"/> becomes the rubric's sort order.
    /// Gated to the blog owner, mirroring the other rubric operations.
    /// </summary>
    /// <returns>The rubrics in their new order</returns>
    Task<IEnumerable<Rubric>> ReorderRubrics(
        Guid blogId, IReadOnlyList<Guid> orderedRubricIds, CancellationToken ct = default);

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
    Task<IEnumerable<UserReference>> GetReaders(Guid blogId, CancellationToken ct = default);

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
    Task<IEnumerable<Blog>> GetSubscribedBlogs(IEnumerable<Guid> blogIds, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where current user is owner, mentor, or assistant
    /// </summary>
    Task<IEnumerable<Blog>> GetOwnBlogsAsync(CancellationToken ct = default);

    /// <summary>
    /// Apply a premoderation transition (send to / remove from premoderation).
    /// Gated Mentor+. The public id is resolved via the repository (ungated) so
    /// a premoderation-pending blog stays reachable for the non-curator mentor.
    /// </summary>
    /// <param name="id">Blog public id (5 letters) or GUID</param>
    /// <param name="transition">Requested transition</param>
    /// <param name="ct">Cancellation token</param>
    Task<Blog> ChangePremoderationAsync(
        string id, BlogPremoderationTransition transition, CancellationToken ct = default);

    /// <summary>
    /// Apply a status transition (start / freeze / finish / close / reopen)
    /// on the blog state machine. Illegal transitions are rejected with 400
    /// before authorization; the transition itself is gated by the blog lead
    /// bucket (owner + assistants). The public id is resolved via the
    /// repository (ungated) so the owner of a premoderation-pending or
    /// private-draft blog can still operate on it.
    /// </summary>
    /// <param name="id">Blog public id (5 letters) or GUID</param>
    /// <param name="transition">Requested transition</param>
    /// <param name="ct">Cancellation token</param>
    Task<Blog> ChangeStatusAsync(
        string id, BlogStatusTransition transition, CancellationToken ct = default);
}
