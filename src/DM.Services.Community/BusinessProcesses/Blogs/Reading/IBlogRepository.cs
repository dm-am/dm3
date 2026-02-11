using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DbBlog = DM.Services.DataAccess.BusinessObjects.Blogs.Blog;
using DbPublication = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DbRubric = DM.Services.DataAccess.BusinessObjects.Blogs.Rubric;

namespace DM.Services.Community.BusinessProcesses.Blogs.Reading;

/// <summary>
/// Repository for blog reading operations
/// </summary>
public interface IBlogRepository
{
    /// <summary>
    /// Count public blogs
    /// </summary>
    Task<int> CountPublicBlogs(CancellationToken ct = default);

    /// <summary>
    /// Get public blogs with paging
    /// </summary>
    Task<IEnumerable<DbBlog>> GetPublicBlogs(PagingData paging, CancellationToken ct = default);

    /// <summary>
    /// Get blogs by owner
    /// </summary>
    Task<IEnumerable<DbBlog>> GetUserBlogs(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by ID
    /// </summary>
    Task<DbBlog?> Get(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get blog by owner login (personal blog)
    /// </summary>
    Task<DbBlog?> GetByOwnerLogin(string login, CancellationToken ct = default);

    /// <summary>
    /// Count publications for a blog
    /// </summary>
    Task<int> CountPublications(Guid blogId, Guid? rubricId, bool includeUnpublished, CancellationToken ct = default);

    /// <summary>
    /// Get publications for a blog
    /// </summary>
    Task<IEnumerable<DbPublication>> GetPublications(
        Guid blogId, Guid? rubricId, bool includeUnpublished, PagingData paging, CancellationToken ct = default);

    /// <summary>
    /// Get publication by ID
    /// </summary>
    Task<DbPublication?> GetPublication(Guid publicationId, CancellationToken ct = default);

    /// <summary>
    /// Get rubrics for a blog
    /// </summary>
    Task<IEnumerable<DbRubric>> GetRubrics(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get popular blogs ordered by participant count
    /// </summary>
    Task<IEnumerable<DbBlog>> GetPopularBlogs(int count, CancellationToken ct = default);
}
