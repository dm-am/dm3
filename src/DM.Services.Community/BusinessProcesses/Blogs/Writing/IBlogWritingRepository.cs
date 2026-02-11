using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Blogs;

namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

/// <summary>
/// Repository for blog write operations
/// </summary>
public interface IBlogWritingRepository
{
    /// <summary>
    /// Create a new blog
    /// </summary>
    Task<Blog> CreateBlog(Blog blog, CancellationToken ct = default);

    /// <summary>
    /// Update blog
    /// </summary>
    Task<Blog> UpdateBlog(Blog blog, CancellationToken ct = default);

    /// <summary>
    /// Delete blog (soft delete)
    /// </summary>
    Task DeleteBlog(Guid blogId, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Create a new rubric
    /// </summary>
    Task<Rubric> CreateRubric(Rubric rubric, CancellationToken ct = default);

    /// <summary>
    /// Update rubric
    /// </summary>
    Task<Rubric> UpdateRubric(Rubric rubric, CancellationToken ct = default);

    /// <summary>
    /// Delete rubric (soft delete)
    /// </summary>
    Task DeleteRubric(Guid rubricId, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Create a new publication
    /// </summary>
    Task<Publication> CreatePublication(Publication publication, CancellationToken ct = default);

    /// <summary>
    /// Update publication
    /// </summary>
    Task<Publication> UpdatePublication(Publication publication, CancellationToken ct = default);

    /// <summary>
    /// Delete publication (soft delete)
    /// </summary>
    Task DeletePublication(Guid publicationId, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Add a participant to a blog
    /// </summary>
    Task AddParticipant(BlogParticipant participant, CancellationToken ct = default);

    /// <summary>
    /// Check if user is already a participant
    /// </summary>
    Task<bool> IsParticipant(Guid blogId, Guid userId, CancellationToken ct = default);
}
