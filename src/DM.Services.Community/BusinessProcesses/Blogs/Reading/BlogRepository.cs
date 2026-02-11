using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;
using DbBlog = DM.Services.DataAccess.BusinessObjects.Blogs.Blog;
using DbPublication = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DbRubric = DM.Services.DataAccess.BusinessObjects.Blogs.Rubric;

namespace DM.Services.Community.BusinessProcesses.Blogs.Reading;

/// <inheritdoc />
internal class BlogRepository : IBlogRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public BlogRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<int> CountPublicBlogs(CancellationToken ct = default)
    {
        return _dbContext.Blogs
            .Where(b => !b.IsRemoved && b.IsPublic)
            .CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DbBlog>> GetPublicBlogs(PagingData paging, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .Include(b => b.Owner)
            .Include(b => b.Publications.Where(p => !p.IsRemoved && p.IsPublished))
            .Where(b => !b.IsRemoved && b.IsPublic)
            .OrderByDescending(b => b.CreatedUtc)
            .Page(paging)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DbBlog>> GetUserBlogs(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .Include(b => b.Owner)
            .Where(b => !b.IsRemoved && b.OwnerId == userId)
            .OrderByDescending(b => b.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DbBlog?> Get(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .Include(b => b.Owner)
            .Include(b => b.Rubrics)
            .FirstOrDefaultAsync(b => b.BlogId == blogId, ct);
    }

    /// <inheritdoc />
    public async Task<DbBlog?> GetByOwnerLogin(string login, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .Include(b => b.Owner)
            .Include(b => b.Rubrics)
            .FirstOrDefaultAsync(b => b.Owner.Login == login, ct);
    }

    /// <inheritdoc />
    public async Task<int> CountPublications(Guid blogId, Guid? rubricId, bool includeUnpublished, CancellationToken ct = default)
    {
        var query = _dbContext.Publications
            .Where(p => p.BlogId == blogId);

        if (!includeUnpublished)
        {
            query = query.Where(p => p.IsPublished);
        }

        if (rubricId.HasValue)
        {
            query = query.Where(p => p.RubricId == rubricId);
        }

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DbPublication>> GetPublications(
        Guid blogId, Guid? rubricId, bool includeUnpublished, PagingData paging, CancellationToken ct = default)
    {
        var query = _dbContext.Publications
            .Include(p => p.Author)
            .Include(p => p.Rubric)
            .Where(p => p.BlogId == blogId);

        if (!includeUnpublished)
        {
            query = query.Where(p => p.IsPublished);
        }

        if (rubricId.HasValue)
        {
            query = query.Where(p => p.RubricId == rubricId);
        }

        return await query
            .OrderByDescending(p => p.PublishedUtc ?? p.CreatedUtc)
            .Page(paging)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DbPublication?> GetPublication(Guid publicationId, CancellationToken ct = default)
    {
        return await _dbContext.Publications
            .Include(p => p.Blog)
            .ThenInclude(b => b.Owner)
            .Include(p => p.Author)
            .Include(p => p.Rubric)
            .FirstOrDefaultAsync(p => p.PublicationId == publicationId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DbRubric>> GetRubrics(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Rubrics
            .Where(r => r.BlogId == blogId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DbBlog>> GetPopularBlogs(int count, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .Include(b => b.Owner)
            .Include(b => b.Publications.Where(p => !p.IsRemoved && p.IsPublished))
            .Where(b => !b.IsRemoved && b.IsPublic)
            .OrderByDescending(b => b.Participants.Count)
            .Take(count)
            .ToListAsync(ct);
    }
}
