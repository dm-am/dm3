using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Dto;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <summary>
/// The catalog of blogs: the filtered, sorted and paged list, and how it is asked
/// </summary>
internal partial class BlogRepository
{
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
            .ProjectToBlog()
            .AsSplitQuery()
            .ToListAsync(ct);

        await FillSubscriberSummary(blogs, filter.CurrentUserId, ct);
        return blogs;
    }

    private IQueryable<DbBlog> GetFilteredQuery(BlogFilter filter)
    {
        var query = _dbContext.Blogs
            .TagWith("DM.Blog.ListPublic");
        query = WhereVisible(query, filter);
        query = WhereInStatus(query, filter);
        query = WhereHostedBy(query, filter);
        query = WhereSearched(query, filter);
        query = WhereDated(query, filter);
        return query;
    }

    /// <summary>
    /// What the list may show at all: no removed blog and no private draft, none
    /// of the owners the viewer hides, and the premoderation gate.
    /// </summary>
    private static IQueryable<DbBlog> WhereVisible(IQueryable<DbBlog> query, BlogFilter filter)
    {
        var excludeOwnerIds = filter.ExcludeOwnerIds;
        var currentUserId = filter.CurrentUserId;
        var premoderationStatuses = filter.PremoderationStatuses;

        // Show Active, Closed, and Draft blogs with public visibility (like games)
        query = query
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

        return query;
    }

    private static IQueryable<DbBlog> WhereInStatus(IQueryable<DbBlog> query, BlogFilter filter)
    {
        // Status filter
        var statuses = filter.Statuses;
        if (statuses?.Count > 0)
        {
            query = query.Where(b => statuses.Contains(b.Status));
        }

        return query;
    }

    private IQueryable<DbBlog> WhereHostedBy(IQueryable<DbBlog> query, BlogFilter filter)
    {
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

        return query;
    }

    private static IQueryable<DbBlog> WhereSearched(IQueryable<DbBlog> query, BlogFilter filter)
    {
        // Text search with fuzzy matching
        var search = filter.Search;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = LikePatterns.Contains(search);
            var searchLower = search.ToLower();
            query = query.Where(b =>
                EF.Functions.ILike(b.Title, searchPattern) ||
                EF.Functions.TrigramsSimilarity(b.Title, searchLower) > LikePatterns.TrigramThreshold);
        }

        return query;
    }

    /// <summary>
    /// The three date ranges of the filter: created, activated and closed.
    /// </summary>
    private static IQueryable<DbBlog> WhereDated(IQueryable<DbBlog> query, BlogFilter filter)
    {
        return query
            .WhereWithin(b => b.CreatedUtc, filter.CreatedFromUtc, filter.CreatedToUtc)
            .WhereWithin(b => b.ActivatedUtc, filter.ActivatedFromUtc, filter.ActivatedToUtc)
            .WhereWithin(b => b.ClosedUtc, filter.ClosedFromUtc, filter.ClosedToUtc);
    }

    private static IOrderedQueryable<DbBlog> ApplySorting(IQueryable<DbBlog> query, string? search, string? sortBy, string? sortOrder)
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

        return sortBy?.ToLowerInvariant() switch
        {
            "title" => query.OrderByDirection(b => b.Title, isAscending),

            "status" => query.OrderByDirection(b => b.Status, isAscending)
                .ThenByDirection(b => b.Status == ModuleStatus.Draft ? b.CreatedUtc :
                                      b.Status == ModuleStatus.Active ? b.ActivatedUtc ?? b.CreatedUtc :
                                      b.ClosedUtc ?? b.CreatedUtc, isAscending),

            "popularity" => query.OrderByDirection(b => b.PopularityScore, isAscending).ThenBy(b => b.Title),

            "activated" => query.OrderByDirection(b => b.ActivatedUtc.HasValue, isAscending)
                .ThenByDirection(b => b.ActivatedUtc, isAscending),

            "closed" => query.OrderByDirection(b => b.ClosedUtc.HasValue, isAscending)
                .ThenByDirection(b => b.ClosedUtc, isAscending),

            _ => query.OrderByDirection(b => b.CreatedUtc, isAscending)
        };
    }
}
