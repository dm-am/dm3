using DM.Domain.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBlogAssistant = DM.Infrastructure.Persistence.Entities.Blog.BlogAssistant;
using DbRubric = DM.Infrastructure.Persistence.Entities.Blog.Rubric;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IBlogRepository" />
internal class BlogRepository : IBlogRepository
{

    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPublicIdService _publicIdService;

    /// <inheritdoc />
    public BlogRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IDateTimeProvider dateTimeProvider,
        IPublicIdService publicIdService)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _dateTimeProvider = dateTimeProvider;
        _publicIdService = publicIdService;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountPublicBlogs(BlogFilter filter, CancellationToken ct = default) =>
        GetFilteredQuery(filter).CountAsync(ct);

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetPublicBlogs(
        PagingData paging, BlogFilter filter, CancellationToken ct = default)
    {
        var query = GetFilteredQuery(filter);

        // Apply ordering. The BlogId tiebreaker makes the order unique so
        // split-query pagination stays deterministic (each collection subquery
        // re-runs the same ORDER BY + OFFSET/FETCH and must hit the same page).
        var orderedQuery = ApplySorting(query, filter.Search, filter.SortBy, filter.SortOrder)
            .ThenBy(b => b.BlogId);

        var blogs = await orderedQuery
            .Page(paging)
            // BlogDto projects three independent collections (Rubrics,
            // Assistants, Tokens); a single query LEFT-JOINs them into a
            // cartesian product that can OOM the reader. Split them.
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToListAsync(ct);

        await FillSubscriberSummary(blogs, filter.CurrentUserId, ct);
        return blogs;
    }

    private IQueryable<DbBlog> GetFilteredQuery(BlogFilter filter)
    {
        var excludeOwnerIds = filter.ExcludeOwnerIds;
        var currentUserId = filter.CurrentUserId;
        var premoderationStatuses = filter.PremoderationStatuses;

        // Show Active, Closed, and Draft blogs with public visibility (like games)
        var query = _dbContext.Blogs
            .TagWith("DM.Blog.ListPublic")
            .Where(b => !b.IsRemoved &&
                (b.Status != ModuleStatus.Draft || b.DraftVisibility == DraftVisibility.Public))
            .Where(b => excludeOwnerIds == null || !excludeOwnerIds.Contains(b.AuthorId));

        // Premoderation visibility (mirrors games): pending blogs are hidden
        // from the public list except for the author, assistants, the
        // assigned curator, and invited users. An explicit filter (Mentor+
        // only, gated by the service) replaces the restriction so reviewers
        // can browse the premoderation queue.
        query = premoderationStatuses?.Count > 0
            ? query.Where(b => premoderationStatuses.Contains(b.PremoderationStatus))
            : query.Where(b =>
                b.PremoderationStatus == PremoderationStatus.Approved ||
                b.AuthorId == currentUserId ||
                b.MentorId == currentUserId ||
                b.Assistants.Any(a => a.UserId == currentUserId) ||
                b.Tokens.Any(t => t.UserId == currentUserId && !t.IsRemoved &&
                    (t.Type == TokenType.BlogAssistantInvitation ||
                     t.Type == TokenType.BlogReaderInvitation)));

        // Status filter
        var statuses = filter.Statuses;
        if (statuses?.Count > 0)
        {
            query = query.Where(b => statuses.Contains(b.Status));
        }

        // Host filter (owner OR assistant, OR logic). Null means the caller is not
        // filtering by host; an empty collection means they are, and nobody
        // matched — answering that with every blog on the site reads the absence
        // of results as the absence of a filter.
        var hostUserIds = filter.HostUserIds;
        if (hostUserIds != null)
        {
            var assistantBlogIds = _dbContext.BlogAssistants
                .Where(a => hostUserIds.Contains(a.UserId))
                .Select(a => a.BlogId);

            query = query.Where(b => hostUserIds.Contains(b.AuthorId) || assistantBlogIds.Contains(b.BlogId));
        }

        // Text search with fuzzy matching
        var search = filter.Search;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = LikePatterns.Contains(search);
            var searchLower = search.ToLower();
            query = query.Where(b =>
                EF.Functions.ILike(b.Title, searchPattern) ||
                EF.Functions.TrigramsSimilarity(b.Title, searchLower) > 0.3);
        }

        // Created date range
        if (filter.CreatedFromUtc.HasValue)
        {
            var createdFromUtc = filter.CreatedFromUtc.Value;
            query = query.Where(b => b.CreatedUtc >= createdFromUtc);
        }
        if (filter.CreatedToUtc.HasValue)
        {
            query = query.WhereAtOrBefore(b => b.CreatedUtc, filter.CreatedToUtc.Value);
        }

        // Activated date range (excludes blogs without ActivatedUtc)
        if (filter.ActivatedFromUtc.HasValue || filter.ActivatedToUtc.HasValue)
        {
            query = query.Where(b => b.ActivatedUtc.HasValue);
            if (filter.ActivatedFromUtc.HasValue)
            {
                var activatedFromUtc = filter.ActivatedFromUtc.Value;
                query = query.Where(b => b.ActivatedUtc >= activatedFromUtc);
            }
            if (filter.ActivatedToUtc.HasValue)
            {
                query = query.WhereAtOrBefore(b => b.ActivatedUtc, filter.ActivatedToUtc.Value);
            }
        }

        // Closed date range (excludes blogs without ClosedUtc)
        if (filter.ClosedFromUtc.HasValue || filter.ClosedToUtc.HasValue)
        {
            query = query.Where(b => b.ClosedUtc.HasValue);
            if (filter.ClosedFromUtc.HasValue)
            {
                var closedFromUtc = filter.ClosedFromUtc.Value;
                query = query.Where(b => b.ClosedUtc >= closedFromUtc);
            }
            if (filter.ClosedToUtc.HasValue)
            {
                query = query.WhereAtOrBefore(b => b.ClosedUtc, filter.ClosedToUtc.Value);
            }
        }

        return query;
    }

    private IOrderedQueryable<DbBlog> ApplySorting(IQueryable<DbBlog> query, string? search, string? sortBy, string? sortOrder)
    {
        var isAscending = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        // Search without explicit sortBy = relevance ranking (best UX)
        if (!string.IsNullOrWhiteSpace(search) && string.IsNullOrEmpty(sortBy))
        {
            var searchLower = search.ToLower();
            var prefixPattern = LikePatterns.StartsWith(search);
            return query
                .OrderByDescending(b => b.Title.ToLower() == searchLower) // Exact match first
                .ThenByDescending(b => EF.Functions.ILike(b.Title, prefixPattern)) // Prefix match
                .ThenByDescending(b => EF.Functions.TrigramsSimilarity(b.Title, searchLower)) // Fuzzy score
                .ThenBy(b => b.Title);
        }

        // Explicit sort selected or no search - use specified sort
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => isAscending
                ? query.OrderBy(b => b.Title)
                : query.OrderByDescending(b => b.Title),

            "status" => isAscending
                ? query.OrderBy(b => b.Status)
                    .ThenBy(b => b.Status == ModuleStatus.Draft ? b.CreatedUtc :
                                 b.Status == ModuleStatus.Active ? b.ActivatedUtc ?? b.CreatedUtc :
                                 b.ClosedUtc ?? b.CreatedUtc)
                : query.OrderByDescending(b => b.Status)
                    .ThenByDescending(b => b.Status == ModuleStatus.Draft ? b.CreatedUtc :
                                           b.Status == ModuleStatus.Active ? b.ActivatedUtc ?? b.CreatedUtc :
                                           b.ClosedUtc ?? b.CreatedUtc),

            "popularity" => isAscending
                ? query.OrderBy(b => b.PopularityScore).ThenBy(b => b.Title)
                : query.OrderByDescending(b => b.PopularityScore).ThenBy(b => b.Title),

            "activated" => isAscending
                ? query.OrderBy(b => b.ActivatedUtc.HasValue).ThenBy(b => b.ActivatedUtc ?? DateTimeOffset.MaxValue)
                : query.OrderByDescending(b => b.ActivatedUtc.HasValue).ThenByDescending(b => b.ActivatedUtc ?? DateTimeOffset.MinValue),

            "closed" => isAscending
                ? query.OrderBy(b => b.ClosedUtc.HasValue).ThenBy(b => b.ClosedUtc ?? DateTimeOffset.MaxValue)
                : query.OrderByDescending(b => b.ClosedUtc.HasValue).ThenByDescending(b => b.ClosedUtc ?? DateTimeOffset.MinValue),

            // default: created
            _ => isAscending
                ? query.OrderBy(b => b.CreatedUtc)
                : query.OrderByDescending(b => b.CreatedUtc)
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetUserBlogs(Guid ownerId, Guid viewerId, CancellationToken ct = default)
    {
        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.ListByUser")
            .Where(b => !b.IsRemoved && b.AuthorId == ownerId)
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            // BlogDto's Rubrics/Assistants/Tokens collections cartesian-explode
            // on a single query; split them (no row limiting here, so EF orders
            // each split by the parent key automatically).
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToListAsync(ct);

        await FillSubscriberSummary(blogs, viewerId, ct);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<BlogDto?> Get(Guid blogId, Guid viewerId, CancellationToken ct = default)
    {
        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blog = await _dbContext.Blogs
            .TagWith("DM.Blog.Get")
            .Where(b => b.BlogId == blogId)
            // AsSplitQuery: BlogDto's Rubrics/Assistants/Tokens collections.
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (blog is not null)
        {
            // The subscriber summary is ignored by the projection and filled only
            // here. This read is the one every authorization decision about the
            // blog is made on, and the reader role is IsViewerSubscriber: without
            // this call a subscriber of a private-draft blog is indistinguishable
            // from a stranger and cannot read the blog they were invited to. The
            // game side fills the same field on its own single-game read for the
            // same reason.
            await FillSubscriberSummary(new[] { blog }, viewerId, ct);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<BlogDto?> GetByPublicId(string publicId, Guid viewerId, CancellationToken ct = default)
    {
        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blog = await _dbContext.Blogs
            .TagWith("DM.Blog.GetByPublicId")
            .Where(b => b.PublicId == publicId)
            // AsSplitQuery: BlogDto's Rubrics/Assistants/Tokens collections.
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (blog is not null)
        {
            // The subscriber summary is ignored by the projection and filled only
            // here. This read is the one every authorization decision about the
            // blog is made on, and the reader role is IsViewerSubscriber: without
            // this call a subscriber of a private-draft blog is indistinguishable
            // from a stranger and cannot read the blog they were invited to. The
            // game side fills the same field on its own single-game read for the
            // same reason.
            await FillSubscriberSummary(new[] { blog }, viewerId, ct);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<BlogDto?> GetByOwnerUsernameAsync(string username, Guid viewerId, CancellationToken ct = default)
    {
        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blog = await _dbContext.Blogs
            .TagWith("DM.Blog.GetByUsername")
            .Where(b => b.Author.Username.ToLower() == username.ToLower())
            // AsSplitQuery: BlogDto's Rubrics/Assistants/Tokens collections.
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (blog is not null)
        {
            // The subscriber summary is ignored by the projection and filled only
            // here. This read is the one every authorization decision about the
            // blog is made on, and the reader role is IsViewerSubscriber: without
            // this call a subscriber of a private-draft blog is indistinguishable
            // from a stranger and cannot read the blog they were invited to. The
            // game side fills the same field on its own single-game read for the
            // same reason.
            await FillSubscriberSummary(new[] { blog }, viewerId, ct);
        }

        return blog;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Rubrics
            .TagWith("DM.Blog.ListRubrics")
            .Where(r => !r.IsRemoved && r.BlogId == blogId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .ProjectTo<Rubric>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(Rubric? rubric, Guid blogId)> GetRubric(Guid rubricId, CancellationToken ct = default)
    {
        var dbRubric = await _dbContext.Rubrics
            .TagWith("DM.Blog.GetRubric")
            .Where(r => r.RubricId == rubricId && !r.IsRemoved)
            .FirstOrDefaultAsync(ct);

        if (dbRubric == null)
        {
            return (null, Guid.Empty);
        }

        var rubric = _mapper.Map<Rubric>(dbRubric);
        return (rubric, dbRubric.BlogId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetPopularBlogs(
        int count, Guid viewerId, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        // Use pre-computed PopularityScore for efficient sorting (updated by PopularityScoreService)
        // Show Active, Closed, and Draft blogs with public visibility (like games)
        // Status grouping: Active first (desc true=first), then Closed, then Draft (false sorts last)
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.Popular")
            .Where(b => !b.IsRemoved &&
                (b.Status != ModuleStatus.Draft || b.DraftVisibility == DraftVisibility.Public))
            // Premoderation-pending blogs never surface in the public widget
            .Where(b => b.PremoderationStatus == PremoderationStatus.Approved)
            .Where(b => excludeOwnerIds == null || !excludeOwnerIds.Contains(b.AuthorId))
            .Where(b => b.PopularityScore > 0)
            .OrderByDescending(b => b.Status == ModuleStatus.Active)
            .ThenByDescending(b => b.Status == ModuleStatus.Closed)
            .ThenByDescending(b => b.PopularityScore)
            .ThenBy(b => b.Title)
            .ThenBy(b => b.BlogId)
            .Take(count)
            // AsSplitQuery: BlogDto's Rubrics/Assistants/Tokens collections.
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToListAsync(ct);

        await FillSubscriberSummary(blogs, viewerId, ct);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetReaders(Guid blogId, CancellationToken ct = default)
    {
        // Readers are now stored in Subscriptions table
        var subscriberIds = await _dbContext.Subscriptions
            .TagWith("DM.Blog.ReaderIds")
            .Where(s => s.TargetType == SubscriptionTargetType.Blog && s.TargetId == blogId)
            .Select(s => s.SubscriberId)
            .ToListAsync(ct);

        return await _dbContext.Users
            .TagWith("DM.Blog.Readers")
            .Where(u => subscriberIds.Contains(u.UserId))
            .Select(u => new GeneralUser
            {
                UserId = u.UserId,
                Username = u.Username,
                Role = u.Role,
                Status = u.Status,
                LastActivityUtc = u.LastActivityUtc,
                Picture = AvatarProjections.From(u.AvatarUpload),
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetAssistants(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.BlogAssistants
            .TagWith("DM.Blog.Assistants")
            .Where(a => a.BlogId == blogId)
            .Select(a => new GeneralUser
            {
                UserId = a.User.UserId,
                Username = a.User.Username,
                Role = a.User.Role,
                Status = a.User.Status,
                LastActivityUtc = a.User.LastActivityUtc,
                Picture = AvatarProjections.From(a.User.AvatarUpload),
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetAssistantsWithJoinDate(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.BlogAssistants
            .TagWith("DM.Blog.AssistantsWithJoinDate")
            .Where(a => a.BlogId == blogId)
            .Select(a => new BlogUser
            {
                User = new GeneralUser
                {
                    UserId = a.User.UserId,
                    Username = a.User.Username,
                    Role = a.User.Role,
                    Status = a.User.Status,
                    LastActivityUtc = a.User.LastActivityUtc,
                    Picture = AvatarProjections.From(a.User.AvatarUpload),
                },
                Role = BlogRole.Assistant,
                JoinedUtc = a.JoinedUtc
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetByIds(
        IEnumerable<Guid> blogIds, Guid viewerId, CancellationToken ct = default)
    {
        var blogIdList = blogIds.ToList();
        if (blogIdList.Count == 0)
            return [];

        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.GetByIds")
            .Where(b => !b.IsRemoved && blogIdList.Contains(b.BlogId))
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            // AsSplitQuery: BlogDto's Rubrics/Assistants/Tokens collections.
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
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

        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.GetOwnBlogs")
            .Where(b => !b.IsRemoved &&
                (b.AuthorId == userId || b.MentorId == userId || assistantBlogIds.Contains(b.BlogId)))
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            // AsSplitQuery: BlogDto's Rubrics/Assistants/Tokens collections.
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToListAsync(ct);

        // The user whose blogs these are is the one asking for them.
        await FillSubscriberSummary(blogs, userId, ct);
        return blogs;
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<BlogDto> CreateBlog(CreateBlogEntity entity, CancellationToken ct = default)
    {
        var blog = new DbBlog
        {
            BlogId = entity.BlogId,
            AuthorId = entity.OwnerId,
            Title = entity.Title,
            Description = entity.Description ?? "",
            DraftVisibility = entity.DraftVisibility,
            CommentsEnabled = entity.CommentsEnabled,
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
        if (!string.IsNullOrWhiteSpace(entity.Description))
            blog.Description = entity.Description;
        if (entity.DraftVisibility.HasValue)
            blog.DraftVisibility = entity.DraftVisibility.Value;
        if (entity.CommentsEnabled.HasValue)
            blog.CommentsEnabled = entity.CommentsEnabled.Value;
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

        blog.UpdatedUtc = entity.UpdatedUtc;
        await _dbContext.SaveChangesAsync(ct);

        // Same as after creation: the writer reads their own write.
        return await Get(entity.BlogId, blog.AuthorId, ct)
               ?? throw new InvalidOperationException("Blog not found after update");
    }

    /// <inheritdoc />
    public async Task DeleteBlog(Guid blogId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var blog = await _dbContext.Blogs.FirstOrDefaultAsync(b => b.BlogId == blogId, ct);
        if (blog != null)
        {
            SoftDelete.Mark(blog, deletedByUserId, _dateTimeProvider.Now);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<Rubric> CreateRubric(CreateRubricEntity entity, CancellationToken ct = default)
    {
        var rubric = new DbRubric
        {
            RubricId = entity.RubricId,
            BlogId = entity.BlogId,
            Title = entity.Title,
            SortOrder = entity.SortOrder,
            IsRemoved = false
        };

        _dbContext.Rubrics.Add(rubric);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Rubrics
            .TagWith("DM.Blog.CreatedRubric")
            .Where(r => r.RubricId == entity.RubricId)
            .ProjectTo<Rubric>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Rubric> UpdateRubric(UpdateRubricEntity entity, CancellationToken ct = default)
    {
        var rubric = await _dbContext.Rubrics.FindAsync([entity.RubricId], ct);
        if (rubric == null)
        {
            throw new InvalidOperationException($"Rubric {entity.RubricId} not found");
        }

        if (!string.IsNullOrWhiteSpace(entity.Title))
            rubric.Title = entity.Title;
        if (entity.SortOrder.HasValue)
            rubric.SortOrder = entity.SortOrder.Value;

        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Rubrics
            .TagWith("DM.Blog.UpdatedRubric")
            .Where(r => r.RubricId == entity.RubricId)
            .ProjectTo<Rubric>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReorderRubrics(
        Guid blogId, IReadOnlyList<Guid> orderedRubricIds, CancellationToken ct = default)
    {
        var rubrics = await _dbContext.Rubrics
            .Where(r => r.BlogId == blogId && !r.IsRemoved)
            .ToListAsync(ct);

        for (var i = 0; i < orderedRubricIds.Count; i++)
        {
            var rubric = rubrics.FirstOrDefault(r => r.RubricId == orderedRubricIds[i]);
            if (rubric != null)
            {
                rubric.SortOrder = i;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, Guid[]>> GetRubricPublicationIds(
        Guid blogId, CancellationToken ct = default)
    {
        var rows = await _dbContext.Publications
            .TagWith("DM.Blog.RubricPublicationIds")
            .Where(p => !p.IsRemoved && p.IsPublished && p.BlogId == blogId && p.RubricId != null)
            .Select(p => new { RubricId = p.RubricId!.Value, p.PublicationId })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.RubricId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PublicationId).ToArray());
    }

    /// <inheritdoc />
    public async Task DeleteRubric(Guid rubricId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var rubric = await _dbContext.Rubrics.FirstOrDefaultAsync(r => r.RubricId == rubricId, ct);
        if (rubric != null)
        {
            SoftDelete.Mark(rubric, deletedByUserId, _dateTimeProvider.Now);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task AddAssistant(AddBlogAssistantEntity entity, CancellationToken ct = default)
    {
        var assistant = new DbBlogAssistant
        {
            BlogId = entity.BlogId,
            UserId = entity.UserId,
            JoinedUtc = entity.JoinedUtc
        };

        _dbContext.BlogAssistants.Add(assistant);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> IsAssistant(Guid blogId, Guid userId, CancellationToken ct = default) =>
        _dbContext.BlogAssistants.AnyAsync(
            a => a.BlogId == blogId && a.UserId == userId,
            ct);

    /// <inheritdoc />
    public async Task<bool> RemoveAssistant(Guid blogId, Guid userId, CancellationToken ct = default)
    {
        var assistant = await _dbContext.BlogAssistants
            .FirstOrDefaultAsync(a => a.BlogId == blogId && a.UserId == userId, ct);

        if (assistant == null)
        {
            return false;
        }

        _dbContext.BlogAssistants.Remove(assistant);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAssistantByUsername(Guid blogId, string username, CancellationToken ct = default)
    {
        var usernameLower = username.ToLower();
        var assistant = await _dbContext.BlogAssistants
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.BlogId == blogId && a.User.Username.ToLower() == usernameLower, ct);

        if (assistant == null)
        {
            return false;
        }

        _dbContext.BlogAssistants.Remove(assistant);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    // ═══ HELPERS ═══

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

        // Total, viewer flag and name preview in one statement — see the twin
        // block in GameRepository.EnrichGamesAsync for the SQL this produces and
        // why the preview needs an explicit order at all.
        var summaries = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.Blog && blogIdSet.Contains(s.TargetId))
            .GroupBy(s => s.TargetId)
            .Select(g => new
            {
                BlogId = g.Key,
                // Distinct subscribers, not subscription rows — see the same count in
                // GameRepository.EnrichGamesAsync for why the two differ.
                Count = g.Select(s => s.SubscriberId).Distinct().Count(),
                ViewerSubscribed = g.Any(s => s.SubscriberId == userId),
                Preview = g.OrderByDescending(s => s.Subscriber.LastActivityUtc != null)
                    .ThenByDescending(s => s.Subscriber.LastActivityUtc)
                    .ThenBy(s => s.SubscriptionId)
                    .Take(SubscriptionPolicy.PreviewCap)
                    .Select(s => s.Subscriber.Username)
                    .ToList(),
            })
            .ToDictionaryAsync(x => x.BlogId, ct);

        // The active count stays its own GROUP BY. Folded into the group above as
        // a conditional Count it degenerates: the predicate is on the subscriber's
        // last activity, so EF emits a correlated subquery that re-joins Users for
        // every blog on the page instead of the single flat scan below.
        var activeSubscribersData = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.Blog &&
                       blogIdSet.Contains(s.TargetId) &&
                       s.Subscriber.LastActivityUtc.HasValue &&
                       s.Subscriber.LastActivityUtc.Value > activeThreshold)
            .GroupBy(s => s.TargetId)
            .Select(g => new { BlogId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BlogId, x => x.Count, ct);

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
