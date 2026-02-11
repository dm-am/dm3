using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Blogs;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

/// <inheritdoc />
internal class BlogWritingRepository : IBlogWritingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BlogWritingRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Blog> CreateBlog(Blog blog, CancellationToken ct = default)
    {
        _dbContext.Blogs.Add(blog);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Blogs
            .Include(b => b.Owner)
            .FirstAsync(b => b.BlogId == blog.BlogId, ct);
    }

    /// <inheritdoc />
    public async Task<Blog> UpdateBlog(Blog blog, CancellationToken ct = default)
    {
        blog.UpdatedUtc = _dateTimeProvider.Now;
        _dbContext.Blogs.Update(blog);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Blogs
            .Include(b => b.Owner)
            .Include(b => b.Rubrics)
            .FirstAsync(b => b.BlogId == blog.BlogId, ct);
    }

    /// <inheritdoc />
    public async Task DeleteBlog(Guid blogId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var blog = await _dbContext.Blogs.FirstOrDefaultAsync(b => b.BlogId == blogId, ct);
        if (blog != null)
        {
            blog.IsRemoved = true;
            blog.DeletedByUserId = deletedByUserId;
            blog.DeletedAtUtc = _dateTimeProvider.Now;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<Rubric> CreateRubric(Rubric rubric, CancellationToken ct = default)
    {
        _dbContext.Rubrics.Add(rubric);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Rubrics
            .Include(r => r.Blog)
            .FirstAsync(r => r.RubricId == rubric.RubricId, ct);
    }

    /// <inheritdoc />
    public async Task<Rubric> UpdateRubric(Rubric rubric, CancellationToken ct = default)
    {
        _dbContext.Rubrics.Update(rubric);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Rubrics
            .Include(r => r.Blog)
            .FirstAsync(r => r.RubricId == rubric.RubricId, ct);
    }

    /// <inheritdoc />
    public async Task DeleteRubric(Guid rubricId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var rubric = await _dbContext.Rubrics.FirstOrDefaultAsync(r => r.RubricId == rubricId, ct);
        if (rubric != null)
        {
            rubric.IsRemoved = true;
            rubric.DeletedByUserId = deletedByUserId;
            rubric.DeletedAtUtc = _dateTimeProvider.Now;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<Publication> CreatePublication(Publication publication, CancellationToken ct = default)
    {
        _dbContext.Publications.Add(publication);

        // Update blog publication count
        var blog = await _dbContext.Blogs.FirstAsync(b => b.BlogId == publication.BlogId, ct);
        blog.PublicationCount++;

        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Publications
            .Include(p => p.Blog)
            .ThenInclude(b => b.Owner)
            .Include(p => p.Author)
            .Include(p => p.Rubric)
            .FirstAsync(p => p.PublicationId == publication.PublicationId, ct);
    }

    /// <inheritdoc />
    public async Task<Publication> UpdatePublication(Publication publication, CancellationToken ct = default)
    {
        publication.ModifiedUtc = _dateTimeProvider.Now;
        _dbContext.Publications.Update(publication);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Publications
            .Include(p => p.Blog)
            .ThenInclude(b => b.Owner)
            .Include(p => p.Author)
            .Include(p => p.Rubric)
            .FirstAsync(p => p.PublicationId == publication.PublicationId, ct);
    }

    /// <inheritdoc />
    public async Task DeletePublication(Guid publicationId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var publication = await _dbContext.Publications
            .Include(p => p.Blog)
            .FirstOrDefaultAsync(p => p.PublicationId == publicationId, ct);

        if (publication != null)
        {
            publication.IsRemoved = true;
            publication.DeletedByUserId = deletedByUserId;
            publication.DeletedAtUtc = _dateTimeProvider.Now;

            // Update blog publication count
            publication.Blog.PublicationCount--;

            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task AddParticipant(BlogParticipant participant, CancellationToken ct = default)
    {
        _dbContext.BlogParticipants.Add(participant);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> IsParticipant(Guid blogId, Guid userId, CancellationToken ct = default) =>
        _dbContext.BlogParticipants.AnyAsync(
            p => p.BlogId == blogId && p.UserId == userId,
            ct);
}
