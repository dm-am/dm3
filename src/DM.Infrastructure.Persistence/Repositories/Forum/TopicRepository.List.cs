using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// The list of topics: of one board or of every board the reader may see, counted,
/// filtered, sorted and paged
/// </summary>
internal partial class TopicRepository
{
    /// <inheritdoc />
    public async Task<int> Count(Guid? boardId, TopicsQuery query, CancellationToken ct = default)
    {
        var dbQuery = _dbContext.Topics
            .TagWith("DM.Forum.TopicsCount")
            .Where(t => !t.IsRemoved);

        dbQuery = ApplyScope(dbQuery, boardId);
        dbQuery = ApplyFilters(dbQuery, query,
            await RemovedAccountScope.IdsIfAsked(_dbContext.Users, query.AuthorUsernames, ct));

        return await dbQuery.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Topic>> Get(Guid? boardId, PagingData? pagingData, TopicsQuery query, CancellationToken ct = default)
    {
        var dbQuery = _dbContext.Topics
            .TagWith(boardId.HasValue ? "DM.Forum.TopicsList" : "DM.Forum.TopicsList.CrossBoard")
            .Where(t => !t.IsRemoved);

        dbQuery = ApplyScope(dbQuery, boardId);
        dbQuery = ApplyFilters(dbQuery, query,
            await RemovedAccountScope.IdsIfAsked(_dbContext.Users, query.AuthorUsernames, ct));

        var sortedDbQuery = SortTopics(dbQuery, query);

        // ProjectTo materialises the Topic DTO and not the entity, so the change
        // tracker is not involved either way. AsNoTracking stays as the default of
        // a read path. See PERFORMANCE.md → "AsNoTracking".
        var topics = await sortedDbQuery
            .Page(pagingData)
            .AsNoTracking()
            .ProjectToTopic()
            .ToArrayAsync(ct);

        await FillListCounts(topics, ct);

        await FillLikes(topics, ct);
        return topics;
    }

    /// <summary>
    /// The order a listing is read in: the key the reader asked for, and the
    /// topic id last.
    /// </summary>
    private IOrderedQueryable<DbTopic> SortTopics(
        IQueryable<DbTopic> dbQuery, TopicsQuery query)
    {
        // Sorted in SQL before the Topic projection, which would otherwise compute
        // LastActivityUtc with a subquery per row.
        var sortBy = (query.SortBy ?? "lastActivity").ToLowerInvariant();
        var sortDesc = string.IsNullOrEmpty(query.SortOrder) ||
                       query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<DbTopic> sortedDbQuery = (sortBy, sortDesc) switch
        {
            ("created", true) => dbQuery.OrderByDescending(t => t.CreatedUtc),
            ("created", false) => dbQuery.OrderBy(t => t.CreatedUtc),
            ("title", true) => dbQuery.OrderByDescending(t => t.Title),
            ("title", false) => dbQuery.OrderBy(t => t.Title),
            // Sort by likes uses a subquery on the Likes table. The same
            // count is materialised below into LikesCount via a single
            // batched GROUP BY — but the sort happens inside SQL so the
            // ordering is consistent with the projected count.
            ("likes", true) => dbQuery.OrderByDescending(t => _dbContext.Likes.Count(l =>
                !l.IsRemoved &&
                l.EntityId == t.TopicId &&
                l.EntityType == LikeEntityType.Topic)),
            ("likes", false) => dbQuery.OrderBy(t => _dbContext.Likes.Count(l =>
                !l.IsRemoved &&
                l.EntityId == t.TopicId &&
                l.EntityType == LikeEntityType.Topic)),
            // Sort by LastActivityUtc = LastComment?.CreatedUtc ?? CreatedUtc.
            // EF translates the coalesce into a single ORDER BY expression.
            (_, true) => dbQuery.OrderByDescending(t =>
                t.LastComment != null ? t.LastComment.CreatedUtc : t.CreatedUtc),
            (_, false) => dbQuery.OrderBy(t =>
                t.LastComment != null ? t.LastComment.CreatedUtc : t.CreatedUtc),
        };

        // For attached topics without custom sort, use AttachOrder then CreatedUtc.
        if (query.IsAttached == true && string.IsNullOrEmpty(query.SortBy))
        {
            sortedDbQuery = dbQuery
                .OrderBy(t => t.AttachOrder ?? int.MaxValue)
                .ThenByDescending(t => t.CreatedUtc);
        }

        // A last key nothing can tie on. Not one key above it is unique: two topics can
        // share a title, a like count, an attach position or the second they were created
        // in. Paging asks for the same order once per page, and rows the order cannot tell
        // apart may be arranged differently between two of those asks, which shows one of
        // them on both pages and the other on neither.
        sortedDbQuery = sortedDbQuery.ThenBy(t => t.TopicId);
        return sortedDbQuery;
    }

    /// <summary>
    /// Backfill <see cref="Topic.TotalCommentsCount"/> and <see cref="Topic.LikesCount"/>
    /// for a page of topics.
    /// </summary>
    private async Task FillListCounts(Topic[] topics, CancellationToken ct)
    {
        // Two batched GROUP BY queries instead of correlated subqueries per row.
        // topicIds is a List<Guid>, not Guid[], for the reason given in LikeBackfill.
        if (topics.Length > 0)
        {
            var topicIds = topics.Select(t => t.Id).ToList();
            var commentCounts = await _dbContext.Comments
                .AsNoTracking()
                .Where(c => topicIds.Contains(c.EntityId) && !c.IsRemoved)
                .GroupBy(c => c.EntityId)
                .Select(g => new { EntityId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EntityId, x => x.Count, ct);

            var likeCounts = await _dbContext.Likes
                .AsNoTracking()
                .Where(l =>
                    !l.IsRemoved &&
                    l.EntityType == LikeEntityType.Topic &&
                    topicIds.Contains(l.EntityId))
                .GroupBy(l => l.EntityId)
                .Select(g => new { EntityId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EntityId, x => x.Count, ct);

            foreach (var topic in topics)
            {
                commentCounts.TryGetValue(topic.Id, out var count);
                topic.TotalCommentsCount = count;
                likeCounts.TryGetValue(topic.Id, out var likes);
                topic.LikesCount = likes;
            }
        }
    }

    /// <summary>
    /// Scope predicate: a single board (per-board listing) or every board
    /// (cross-board listing, the user profile's Topics tab).
    /// </summary>
    private static IQueryable<DbTopic> ApplyScope(IQueryable<DbTopic> dbQuery, Guid? boardId) =>
        boardId.HasValue ? dbQuery.Where(t => t.BoardId == boardId.Value) : dbQuery;

    /// <summary>
    /// Shared filter predicates for both count and listing queries — kept
    /// in one place so a new filter (added to TopicsQuery) automatically
    /// affects pagination AND the listing without diverging.
    /// </summary>
    /// <param name="dbQuery">Topics in scope</param>
    /// <param name="query">What the reader asked for</param>
    /// <param name="authorIds">
    /// The authors meant when the names asked include "Аккаунт удален" - the removed
    /// accounts and the live ones named alongside (<see cref="RemovedAccountScope" />);
    /// null otherwise.
    /// </param>
    private static IQueryable<DbTopic> ApplyFilters(
        IQueryable<DbTopic> dbQuery, TopicsQuery query, IReadOnlyCollection<Guid>? authorIds)
    {
        if (query.IsAttached.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.IsAttached == query.IsAttached.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchPattern = LikePatterns.Contains(query.Search.Trim());
            dbQuery = dbQuery.Where(t => EF.Functions.ILike(t.Title, searchPattern));
        }

        if (authorIds is not null)
        {
            // The profile of removed accounts asks for its topics this way: by the
            // author column, joining no account.
            dbQuery = dbQuery.Where(t => authorIds.Contains(t.AuthorId));
        }
        else if (query.AuthorUsernames is { Count: > 0 })
        {
            // The listing lifts the account filter to keep the topics of removed
            // accounts (RemovedAuthors), so a removed login is refused here in the
            // open: asked by the old name, the list would otherwise hand back
            // exactly what the removal took off that name.
            var authorNames = query.AuthorUsernames.Select(a => a.ToLowerInvariant()).ToArray();
            dbQuery = dbQuery.Where(t => !t.Author.IsRemoved && authorNames.Contains(t.Author.Username.ToLower()));
        }

        if (query.CreatedFromUtc.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.CreatedUtc >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            dbQuery = dbQuery.WhereAtOrBefore(t => t.CreatedUtc, query.CreatedToUtc.Value);
        }

        return dbQuery;
    }
}
