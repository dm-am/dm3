using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blogs;

public interface IBlogRepository
{
    /// <summary>
    /// Count public blogs matching the filter. Takes the same filter object as
    /// <see cref="GetPublicBlogs" /> so the total cannot describe a different
    /// set than the page does. The service has resolved the host ids and the
    /// current user already.
    /// </summary>
    Task<int> CountPublicBlogs(BlogFilter filter, CancellationToken ct = default);

    Task<IEnumerable<Blog>> GetPublicBlogs(
        PagingData paging, BlogFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where <paramref name="ownerId" /> is owner or assistant.
    /// <paramref name="viewerId" /> is who is reading, for
    /// <see cref="Blog.IsViewerSubscriber" /> (<see cref="Guid.Empty" /> for a
    /// guest): not the owner on any page but the user's own.
    /// </summary>
    Task<IEnumerable<Blog>> GetUserBlogs(Guid ownerId, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// <paramref name="viewerId" /> is the user the read is on behalf of
    /// (<see cref="Guid.Empty" /> for a guest); every authorization decision about
    /// a private draft is made on this read, and the reader role is
    /// <see cref="Blog.IsViewerSubscriber" />.
    /// </summary>
    Task<Blog?> Get(Guid blogId, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by public ID (5-letter URL identifier), the viewer as in <see cref="Get" />
    /// </summary>
    Task<Blog?> GetByPublicId(string publicId, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner username (personal blog), the viewer as in <see cref="Get" />
    /// </summary>
    Task<Blog?> GetByOwnerUsernameAsync(string username, Guid viewerId, CancellationToken ct = default);

    Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get rubric by ID with its blog ID
    /// </summary>
    Task<(Rubric? rubric, Guid blogId)> GetRubric(Guid rubricId, CancellationToken ct = default);

    /// <summary>
    /// Get popular blogs ordered by subscriber count, without the blogs of
    /// <paramref name="excludeOwnerIds" /> (blacklist filtering)
    /// </summary>
    Task<IEnumerable<Blog>> GetPopularBlogs(
        int count, Guid viewerId, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

    Task<IEnumerable<BlogUser>> GetAssistantsWithJoinDate(Guid blogId, CancellationToken ct = default);

    Task<IEnumerable<Blog>> GetByIds(IEnumerable<Guid> blogIds, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where user is owner, mentor, or assistant.
    /// The user is the reader here, so they are also who
    /// <see cref="Blog.IsViewerSubscriber" /> is filled for.
    /// </summary>
    Task<IEnumerable<Blog>> GetOwnBlogs(Guid userId, CancellationToken ct = default);

    Task<Blog> CreateBlog(CreateBlogEntity entity, CancellationToken ct = default);

    Task<Blog> UpdateBlog(UpdateBlogEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Delete blog (soft delete) and drop the subscriptions to it
    /// </summary>
    Task DeleteBlog(Guid blogId, Guid deletedByUserId, CancellationToken ct = default);

    Task<Rubric> CreateRubric(CreateRubricEntity entity, CancellationToken ct = default);

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
    /// Get the ids of published publications grouped by rubric for a blog, for
    /// the per-rubric unread counters (a rubric is not itself an unread-counter entity).
    /// </summary>
    Task<IDictionary<Guid, Guid[]>> GetRubricPublicationIds(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Delete rubric (soft delete)
    /// </summary>
    Task DeleteRubric(Guid rubricId, Guid deletedByUserId, CancellationToken ct = default);

    Task AddAssistant(AddBlogAssistantEntity entity, CancellationToken ct = default);

    Task<bool> IsAssistant(Guid blogId, Guid userId, CancellationToken ct = default);

    Task<bool> RemoveAssistant(Guid blogId, Guid userId, CancellationToken ct = default);

    Task<bool> RemoveAssistantByUsername(Guid blogId, string username, CancellationToken ct = default);
}
