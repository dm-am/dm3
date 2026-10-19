using DM.Domain.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Subscriptions;
using Microsoft.EntityFrameworkCore;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IBlogRepository" />
internal partial class BlogRepository : IBlogRepository
{

    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly PublicIdService _publicIdService;

    public BlogRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        PublicIdService publicIdService)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _publicIdService = publicIdService;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetUserBlogs(Guid ownerId, Guid viewerId, CancellationToken ct = default)
    {
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.ListByUser")
            .Where(b => !b.IsRemoved && b.AuthorId == ownerId)
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            .ProjectToBlog()
            .AsSplitQuery()
            .ToListAsync(ct);

        await FillSubscriberSummary(blogs, viewerId, ct);
        return blogs;
    }

    /// <inheritdoc />
    public Task<BlogDto?> Get(Guid blogId, Guid viewerId, CancellationToken ct = default) =>
        ReadOne(_dbContext.Blogs.TagWith("DM.Blog.Get").Where(b => b.BlogId == blogId), viewerId, ct);

    /// <inheritdoc />
    public Task<BlogDto?> GetByPublicId(string publicId, Guid viewerId, CancellationToken ct = default) =>
        ReadOne(_dbContext.Blogs.TagWith("DM.Blog.GetByPublicId").Where(b => b.PublicId == publicId), viewerId, ct);

    /// <inheritdoc />
    public Task<BlogDto?> GetByOwnerUsernameAsync(string username, Guid viewerId, CancellationToken ct = default) =>
        // ProjectToBlog lifts the account filter (RemovedAuthors): a removed owner's login finds no blog.
        ReadOne(_dbContext.Blogs.TagWith("DM.Blog.GetByUsername")
            .Where(b => !b.Author.IsRemoved && b.Author.Username.ToLower() == username.ToLower()), viewerId, ct);

    /// <summary>
    /// One blog as the viewer reads it. The subscriber summary is filled here
    /// because authorization reads IsViewerSubscriber off this blog.
    /// </summary>
    private async Task<BlogDto?> ReadOne(IQueryable<DbBlog> blogs, Guid viewerId, CancellationToken ct)
    {
        var blog = await blogs
            .ProjectToBlog()
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (blog is not null)
        {
            await FillSubscriberSummary(new[] { blog }, viewerId, ct);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetPopularBlogs(
        int count, Guid viewerId, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        // Active first, then Closed, then Draft (false sorts last)
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.Popular")
            .Where(b => !b.IsRemoved)
            .Where(BlogAccessibilityFilters.PubliclyVisible)
            .Where(b => excludeOwnerIds == null || !excludeOwnerIds.Contains(b.AuthorId))
            .Where(b => b.PopularityScore > 0)
            .OrderByDescending(b => b.Status == ModuleStatus.Active)
            .ThenByDescending(b => b.Status == ModuleStatus.Closed)
            .ThenByDescending(b => b.PopularityScore)
            .ThenBy(b => b.Title)
            .ThenBy(b => b.BlogId)
            .Take(count)
            .ProjectToBlog()
            .AsSplitQuery()
            .ToListAsync(ct);

        await FillSubscriberSummary(blogs, viewerId, ct);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetByIds(
        IEnumerable<Guid> blogIds, Guid viewerId, CancellationToken ct = default)
    {
        var blogIdList = blogIds.ToList();
        if (blogIdList.Count == 0)
            return [];

        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.GetByIds")
            .Where(b => !b.IsRemoved && blogIdList.Contains(b.BlogId))
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            .ProjectToBlog()
            .AsSplitQuery()
            .ToListAsync(ct);

        await FillSubscriberSummary(blogs, viewerId, ct);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetOwnBlogs(Guid userId, CancellationToken ct = default)
    {
        // Get blogs where user is owner, mentor, or assistant
        var assistantBlogIds = await _dbContext.BlogAssistants
            .Where(a => a.UserId == userId)
            .Select(a => a.BlogId)
            .ToListAsync(ct);

        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.GetOwnBlogs")
            .Where(b => !b.IsRemoved &&
                (b.AuthorId == userId || b.MentorId == userId || assistantBlogIds.Contains(b.BlogId)))
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            .ProjectToBlog()
            .AsSplitQuery()
            .ToListAsync(ct);

        // The user whose blogs these are is the one asking for them.
        await FillSubscriberSummary(blogs, userId, ct);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<BlogDto> CreateBlog(CreateBlogEntity entity, CancellationToken ct = default)
    {
        var blog = new DbBlog
        {
            BlogId = entity.BlogId,
            AuthorId = entity.OwnerId,
            Title = entity.Title,
            Description = entity.Description ?? "",
            PremoderationStatus = entity.PremoderationStatus,
            DraftVisibility = entity.DraftVisibility,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false
        };

        // The readable address is taken before the insert instead of being stamped by
        // a second SaveChanges — see SerialNumberAllocator for what that pair cost.
        blog.SerialNumber = await SerialNumberAllocator.NextAsync<DbBlog>(_dbContext, ct);
        blog.PublicId = _publicIdService.Encode(blog.SerialNumber);

        _dbContext.Blogs.Add(blog);
        await _dbContext.SaveChangesAsync(ct);

        // The owner reads their own write, and an owner is never a subscriber of
        // their own blog — the viewer flag this fills is false either way.
        return await Get(entity.BlogId, entity.OwnerId, ct)
               ?? throw new InvalidOperationException("Blog not found after creation");
    }

    /// <inheritdoc />
    public async Task<BlogDto> UpdateBlog(UpdateBlogEntity entity, CancellationToken ct = default)
    {
        var blog = await _dbContext.Blogs.FindAsync([entity.BlogId], ct);
        if (blog == null)
        {
            throw new InvalidOperationException($"Blog {entity.BlogId} not found");
        }

        if (!string.IsNullOrWhiteSpace(entity.Title))
            blog.Title = entity.Title;
        // Null keeps the description, an empty one clears it: the description is optional.
        if (entity.Description != null)
            blog.Description = entity.Description;
        if (entity.DraftVisibility.HasValue)
            blog.DraftVisibility = entity.DraftVisibility.Value;
        if (entity.Status.HasValue)
            blog.Status = entity.Status.Value;
        if (entity.ClosedReason.HasValue)
            blog.ClosedReason = entity.ClosedReason.Value;
        if (entity.ActivatedUtc.HasValue)
            blog.ActivatedUtc = entity.ActivatedUtc.Value;
        if (entity.ClosedUtc.HasValue)
            blog.ClosedUtc = entity.ClosedUtc.Value;
        if (entity.ClearClosedUtc)
            blog.ClosedUtc = null;
        if (entity.PremoderationStatus.HasValue)
            blog.PremoderationStatus = entity.PremoderationStatus.Value;
        if (entity.SetMentorId)
            blog.MentorId = entity.MentorId;

        blog.UpdatedUtc = entity.ModifiedUtc;
        await _dbContext.SaveChangesAsync(ct);

        // Same as after creation: the writer reads their own write.
        return await Get(entity.BlogId, blog.AuthorId, ct)
               ?? throw new InvalidOperationException("Blog not found after update");
    }

    /// <inheritdoc />
    /// <remarks>
    /// The subscriptions to the blog go with it, in the same transaction. Nothing
    /// brings a deleted blog back, so a subscription that outlived it could never
    /// deliver anything: it only stood in the reader's list as a nameless row
    /// ("Блог n/a") that was theirs to clean up.
    /// </remarks>
    public async Task DeleteBlog(Guid blogId, Guid deletedByUserId, CancellationToken ct = default)
    {
        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            var blog = await _dbContext.Blogs.FirstOrDefaultAsync(b => b.BlogId == blogId, cancellation);
            if (blog != null)
            {
                SoftDelete.Mark(blog, deletedByUserId, _dateTimeProvider.Now);
                await _dbContext.SaveChangesAsync(cancellation);

                await _dbContext.Subscriptions
                    .Where(s => s.TargetType == SubscriptionTargetType.Blog && s.TargetId == blogId)
                    .ExecuteDeleteAsync(cancellation);
            }

            await transaction.CommitAsync(cancellation);
        }, ct);
    }

    /// <summary>
    /// Fill the subscriber summary — total, whether this viewer subscribes, the
    /// capped name preview, and the active-subscriber count — on blogs the
    /// projection cannot produce it for.
    /// </summary>
    /// <param name="blogs">Blogs to hydrate</param>
    /// <param name="userId">
    /// The user the read is on behalf of; only
    /// <see cref="BlogDto.IsViewerSubscriber" /> depends on it.
    /// <see cref="Guid.Empty" /> for an anonymous read.
    /// </param>
    /// <param name="ct">Cancellation token</param>
    private async Task FillSubscriberSummary(IList<BlogDto> blogs, Guid userId, CancellationToken ct)
    {
        if (blogs.Count == 0) return;

        var blogIdSet = blogs.Select(b => b.Id).ToHashSet();
        var activeThreshold = _dateTimeProvider.Now - ActivityPolicy.ActivePeriod;

        // Total, viewer flag and name preview in one statement.
        var summaries = await SubscriberSummaries.ByTarget(
            _dbContext.Subscriptions, SubscriptionTargetType.Blog, blogIdSet, userId, ct);

        var activeSubscribersData = await SubscriberSummaries.ActiveByTarget(
            _dbContext.Subscriptions, SubscriptionTargetType.Blog, blogIdSet, activeThreshold, ct);

        foreach (var blog in blogs)
        {
            var summary = summaries.GetValueOrDefault(blog.Id);
            blog.SubscribersCount = summary?.Count ?? 0;
            blog.IsViewerSubscriber = summary?.ViewerSubscribed ?? false;
            blog.SubscriberUsernames = summary?.Preview ?? [];
            blog.ActiveSubscribersCount = activeSubscribersData.GetValueOrDefault(blog.Id, 0);
        }
    }
}
