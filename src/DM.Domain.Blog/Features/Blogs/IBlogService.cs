using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Statuses;

namespace DM.Domain.Blog.Features.Blogs;

public interface IBlogService
{
    /// <summary>
    /// Get public blogs with paging. Resolves the filter's host usernames into
    /// user ids and role-gates its premoderation status before querying, so
    /// callers pass what the request said and nothing more. An empty filter
    /// means the whole public list.
    /// </summary>
    Task<(IEnumerable<Blog> blogs, PagingResult paging)> GetPublicBlogs(
        PagingQuery query, BlogFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Get popular blogs ordered by subscriber count, without the blogs of
    /// <paramref name="excludeOwnerIds" /> (blacklist filtering)
    /// </summary>
    Task<IEnumerable<Blog>> GetPopularBlogs(
        int count = 5, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where user is owner or assistant
    /// </summary>
    Task<IEnumerable<Blog>> GetUserBlogs(string username, CancellationToken ct = default);

    /// <summary>
    /// A blog the reader may not see answers the same as one that is not
    /// there: 404, same body.
    /// </summary>
    Task<Blog> GetAsync(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by public ID (5-letter URL identifier). Answers on a blog the
    /// reader may not see exactly as on an alias nobody has taken.
    /// </summary>
    Task<Blog> GetByPublicIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID (for authorization checks, skips draft visibility)
    /// </summary>
    Task<Blog> GetUnfiltered(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// The blog as the caller sees it, for a read or for a move he makes on it as
    /// its reader.
    /// </summary>
    /// <remarks>
    /// A blog the caller may not open answers exactly as one that is not there.
    /// A 403 instead would tell a stranger that the identifier resolves and that
    /// what it resolves to is a private draft or a blog under premoderation, and a
    /// move let through would write a subscription into a blog hidden from the
    /// one who made it.
    /// </remarks>
    Task<Blog> GetVisibleAsync(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Whether the reader the current request is on behalf of may see the blog at
    /// all: the private-draft gate and the premoderation gate that
    /// <see cref="GetAsync" /> throws on, asked instead of thrown. A blog that is
    /// not there answers the same as one the reader may not open.
    /// </summary>
    /// <remarks>
    /// For the callers whose refusal has to be a 404 and not a 403. What lives
    /// inside a blog asks it about its parent: a publication row on its own cannot
    /// tell whether it is readable, and the id of one is handed out by the profile widget.
    /// </remarks>
    Task<bool> IsVisibleToViewerAsync(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner username (personal blog). Answers on a blog the reader
    /// may not see exactly as on a user who keeps none.
    /// </summary>
    Task<Blog> GetByOwnerUsernameAsync(string username, CancellationToken ct = default);

    Task AddAssistant(Guid blogId, Guid userId, CancellationToken ct = default);

    Task<Blog> Create(CreateBlog createBlog, CancellationToken ct = default);

    Task<Blog> Update(UpdateBlog updateBlog, CancellationToken ct = default);

    Task Delete(Guid blogId, CancellationToken ct = default);

    Task<Rubric> CreateRubric(CreateRubric createRubric, CancellationToken ct = default);

    /// <summary>
    /// Rename a rubric (and optionally change its sort order)
    /// </summary>
    Task<Rubric> UpdateRubric(UpdateRubric updateRubric, CancellationToken ct = default);

    /// <summary>
    /// Replace the order of a blog's rubrics. The position of each id in
    /// <paramref name="orderedRubricIds"/> becomes the rubric's sort order.
    /// </summary>
    /// <remarks>
    /// The whole order, not a correction to it: <paramref name="orderedRubricIds"/>
    /// names every rubric of the blog exactly once, and a list that does not is
    /// refused.
    /// </remarks>
    /// <returns>The rubrics in their new order</returns>
    Task<IEnumerable<Rubric>> ReorderRubrics(
        Guid blogId, IReadOnlyList<Guid> orderedRubricIds, CancellationToken ct = default);

    Task DeleteRubric(Guid rubricId, CancellationToken ct = default);

    Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blogs by IDs (for subscribed blogs), the ones the caller may open
    /// </summary>
    Task<IEnumerable<Blog>> GetSubscribedBlogs(IEnumerable<Guid> blogIds, CancellationToken ct = default);

    /// <summary>
    /// Get blogs where current user is owner, mentor, or assistant
    /// </summary>
    Task<IEnumerable<Blog>> GetOwnBlogsAsync(CancellationToken ct = default);

    /// <summary>
    /// Apply a premoderation transition (send to / remove from premoderation).
    /// Gated Mentor+. The public id is resolved via the repository (ungated) so
    /// a premoderation-pending blog stays reachable for a mentor who is not the blog's.
    /// <paramref name="id" /> is the public id (5 letters) or the GUID.
    /// </summary>
    Task<Blog> ChangePremoderationAsync(
        string id, ModulePremoderationTransition transition, CancellationToken ct = default);

    /// <summary>
    /// Apply a status transition (start / freeze / finish / close / reopen)
    /// on the blog state machine. Illegal transitions are rejected with 400
    /// before authorization; the transition itself is gated by the blog lead
    /// bucket (owner + assistants). <paramref name="id" /> is the public id
    /// (5 letters) or the GUID.
    /// </summary>
    Task<Blog> ChangeStatusAsync(
        string id, ModuleStatusTransition transition, CancellationToken ct = default);
}
