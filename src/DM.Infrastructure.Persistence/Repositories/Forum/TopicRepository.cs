using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// Repository for forum topics (unified CRUD operations)
/// </summary>
internal class TopicRepository : ITopicRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TopicRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    private static readonly Guid NewsBoardId = Guid.Parse("00000000-0000-0000-0000-000000000008");
    private static readonly Guid ErrorsBoardId = Guid.Parse("00000000-0000-0000-0000-000000000006");

    // --- READ ---

    /// <inheritdoc />
    public Task<int> Count(Guid? boardId, BoardAccessPolicy accessPolicy, TopicsQuery query, CancellationToken ct = default)
    {
        var dbQuery = _dbContext.Topics
            .TagWith("DM.Forum.TopicsCount")
            .Where(t => !t.IsRemoved);

        dbQuery = ApplyScope(dbQuery, boardId, accessPolicy);
        dbQuery = ApplyFilters(dbQuery, query);

        return dbQuery.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Topic>> Get(Guid? boardId, BoardAccessPolicy accessPolicy, PagingData? pagingData, TopicsQuery query, CancellationToken ct = default)
    {
        var dbQuery = _dbContext.Topics
            .TagWith(boardId.HasValue ? "DM.Forum.TopicsList" : "DM.Forum.TopicsList.CrossBoard")
            .Where(t => !t.IsRemoved);

        dbQuery = ApplyScope(dbQuery, boardId, accessPolicy);
        dbQuery = ApplyFilters(dbQuery, query);

        // Sort by LastActivityUtc needs the raw LastComment relation so
        // we sort in SQL BEFORE the Topic DTO projection. AutoMapper's
        // projected query would otherwise generate an extra subquery per
        // row to compute the same value.
        var sortBy = (query.SortBy ?? "lastActivity").ToLowerInvariant();
        var sortDesc = string.IsNullOrEmpty(query.SortOrder) ||
                       query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<Entities.Forum.Topic> sortedDbQuery = (sortBy, sortDesc) switch
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
                l.EntityType == Domain.Core.Enums.LikeEntityType.Topic)),
            ("likes", false) => dbQuery.OrderBy(t => _dbContext.Likes.Count(l =>
                !l.IsRemoved &&
                l.EntityId == t.TopicId &&
                l.EntityType == Domain.Core.Enums.LikeEntityType.Topic)),
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

        // Read-only projection: AsNoTracking avoids EF Core's change
        // tracker overhead. See PERFORMANCE.md → "AsNoTracking".
        var topics = await sortedDbQuery
            .Page(pagingData)
            .AsNoTracking()
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        // Fill TotalCommentsCount + LikesCount in two batched GROUP BY
        // queries instead of correlated subqueries per row (PERFORMANCE.md
        // → "Avoid inline aggregations"). For a page of N topics we trade
        // 2N subqueries for 2 indexed lookups.
        //
        // topicIds is a List<Guid>, NOT Guid[] — EF Core's LINQ
        // translator has a Guid[] edge case that throws TypeLoadException
        // on the ReadOnlySpan<Guid> interpreter path. List<Guid> avoids it.
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
                    l.EntityType == Domain.Core.Enums.LikeEntityType.Topic &&
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

        return topics;
    }

    /// <summary>
    /// Scope predicate: either a single board (per-board listing) or all
    /// boards visible to the current viewer (cross-board listing — used by
    /// the user profile's Topics tab). The access-policy bitmask is the
    /// same one already used by <see cref="Get(Guid, BoardAccessPolicy, CancellationToken)"/>
    /// and the single-topic lookup, so visibility is consistent across paths.
    /// </summary>
    private static IQueryable<Entities.Forum.Topic> ApplyScope(
        IQueryable<Entities.Forum.Topic> dbQuery, Guid? boardId, BoardAccessPolicy accessPolicy)
    {
        return boardId.HasValue
            ? dbQuery.Where(t => t.BoardId == boardId.Value)
            : dbQuery.Where(t => (t.Board.ViewPolicy & accessPolicy) != BoardAccessPolicy.None);
    }

    /// <summary>
    /// Shared filter predicates for both count and listing queries — kept
    /// in one place so a new filter (added to TopicsQuery) automatically
    /// affects pagination AND the listing without diverging.
    /// </summary>
    private static IQueryable<Entities.Forum.Topic> ApplyFilters(
        IQueryable<Entities.Forum.Topic> dbQuery, TopicsQuery query)
    {
        if (query.IsAttached.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.IsAttached == query.IsAttached.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim();
            dbQuery = dbQuery.Where(t => EF.Functions.ILike(t.Title, $"%{searchTerm}%"));
        }

        if (query.Authors is { Count: > 0 })
        {
            var authorNames = query.Authors.Select(a => a.ToLowerInvariant()).ToArray();
            dbQuery = dbQuery.Where(t => authorNames.Contains(t.Author.Username.ToLower()));
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

    /// <inheritdoc />
    public async Task<Topic?> Get(Guid topicId, BoardAccessPolicy accessPolicy, CancellationToken ct = default)
    {
        var topic = await _dbContext.Topics
            .TagWith("DM.Forum.Topic")
            .Where(t => !t.IsRemoved && t.TopicId == topicId &&
                        (t.Board.ViewPolicy & accessPolicy) != BoardAccessPolicy.None)
            .AsNoTracking()
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        await FillCounts(topic, ct);
        return topic;
    }

    /// <inheritdoc />
    /// <inheritdoc />
    // IgnoreQueryFilters is the whole point of these two probes: the soft-delete
    // filter is global, so without it they can only ever see rows that are NOT
    // removed — which is exactly the case the caller already knows about. With
    // the filter applied the 410 branch is unreachable and every deleted topic
    // reports 404.
    public Task<bool> Exists(Guid topicId, CancellationToken ct = default) =>
        _dbContext.Topics
            .IgnoreQueryFilters()
            .TagWith("DM.Forum.TopicExists")
            .AnyAsync(t => t.TopicId == topicId, ct);

    /// <inheritdoc />
    public Task<bool> ExistsByBoardAndNumber(Guid boardId, int topicNumber, CancellationToken ct = default) =>
        _dbContext.Topics
            .IgnoreQueryFilters()
            .TagWith("DM.Forum.TopicExistsByNumber")
            .AnyAsync(t => t.BoardId == boardId && t.TopicNumber == topicNumber, ct);

    public async Task<Topic?> GetByBoardAndNumber(Guid boardId, int topicNumber, BoardAccessPolicy accessPolicy, CancellationToken ct = default)
    {
        var topic = await _dbContext.Topics
            .TagWith("DM.Forum.TopicByBoardAndNumber")
            .Where(t => !t.IsRemoved && t.BoardId == boardId && t.TopicNumber == topicNumber &&
                        (t.Board.ViewPolicy & accessPolicy) != BoardAccessPolicy.None)
            .AsNoTracking()
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        await FillCounts(topic, ct);
        return topic;
    }

    /// <inheritdoc />
    public async Task<Topic?> GetBestUserTopic(Guid authorId, BoardAccessPolicy accessPolicy, CancellationToken ct = default)
    {
        // Single-query "best" lookup: sort by the same likes subquery
        // pattern used by the listing path, take the top row, project to
        // the Topic DTO. Soft-deleted topics and topics on boards the
        // viewer cannot see are filtered out so the profile widget never
        // surfaces hidden content.
        var topic = await _dbContext.Topics
            .TagWith("DM.Forum.GetBestUserTopic")
            .Where(t => !t.IsRemoved && t.AuthorId == authorId &&
                        (t.Board.ViewPolicy & accessPolicy) != BoardAccessPolicy.None)
            .OrderByDescending(t => _dbContext.Likes.Count(l =>
                !l.IsRemoved &&
                l.EntityId == t.TopicId &&
                l.EntityType == Domain.Core.Enums.LikeEntityType.Topic))
            // Tie-breaker: newer-first so two zero-like topics still produce
            // a deterministic result rather than relying on insertion order.
            .ThenByDescending(t => t.CreatedUtc)
            .AsNoTracking()
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        await FillCounts(topic, ct);
        return topic;
    }

    /// <summary>
    /// Backfill <see cref="Topic.TotalCommentsCount"/> and
    /// <see cref="Topic.LikesCount"/> for a single topic — the same counts
    /// the list path materialises via batched GROUP BY. The mapping profile
    /// intentionally ignores these to avoid per-row correlated subqueries, so
    /// the single-topic page would otherwise show 0. See the list path above
    /// and TopicMappingProfile for the rationale.
    /// </summary>
    private async Task FillCounts(Topic? topic, CancellationToken ct)
    {
        if (topic == null)
        {
            return;
        }

        topic.TotalCommentsCount = await _dbContext.Comments
            .AsNoTracking()
            .Where(c => c.EntityId == topic.Id && !c.IsRemoved)
            .CountAsync(ct);

        topic.LikesCount = await _dbContext.Likes
            .AsNoTracking()
            .Where(l =>
                !l.IsRemoved &&
                l.EntityType == Domain.Core.Enums.LikeEntityType.Topic &&
                l.EntityId == topic.Id)
            .CountAsync(ct);
    }

    // --- WRITE ---

    /// <inheritdoc />
    public async Task<Topic> Create(CreateTopicEntity createTopic, Guid authorId, Guid boardId, CancellationToken ct = default)
    {
        var topicId = _guidFactory.Create();
        var now = _dateTimeProvider.Now;

        // Calculate next TopicNumber for this board
        var maxTopicNumber = await _dbContext.Topics
            .TagWith("DM.Forum.MaxTopicNumber")
            .Where(t => t.BoardId == boardId)
            .Select(t => (int?)t.TopicNumber)
            .MaxAsync(ct) ?? 0;
        var topicNumber = maxTopicNumber + 1;

        var topic = new Entities.Forum.Topic
        {
            TopicId = topicId,
            BoardId = boardId,
            TopicNumber = topicNumber,
            AuthorId = authorId,
            Title = createTopic.Title.Trim(),
            Text = createTopic.Text.Trim(),
            CreatedUtc = now,
            IsRemoved = false,
            IsClosed = false,
            IsAttached = false,
            CommentCount = 0
        };

        _dbContext.Topics.Add(topic);

        // Update board's last topic (denormalized fields)
        var board = await _dbContext.Boards.FindAsync([boardId], ct);
        if (board != null)
        {
            board.LastTopicId = topicId;
            board.LastTopicNumber = topicNumber;
            board.LastTopicTitle = topic.Title;
            board.LastTopicAuthorId = authorId;
            board.LastTopicCreatedUtc = now;
            board.TopicsCount++;
        }

        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Topics
            .TagWith("DM.Forum.CreatedTopic")
            .Where(t => t.TopicId == topicId)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Topic> Update(UpdateTopicEntity updateTopic, Guid? boardId = null)
    {
        var topic = await _dbContext.Topics.FindAsync(updateTopic.TopicId);
        if (topic != null)
        {
            if (!string.IsNullOrEmpty(updateTopic.Title))
            {
                topic.Title = updateTopic.Title.Trim();
            }

            // null = don't update (the UpdateTopic contract); an empty
            // string is a deliberate clear — topic text is optional.
            if (updateTopic.Text != null)
            {
                topic.Text = updateTopic.Text.Trim();
            }

            if (updateTopic.IsClosed.HasValue)
            {
                topic.IsClosed = updateTopic.IsClosed.Value;
            }

            if (updateTopic.IsAttached.HasValue)
            {
                topic.IsAttached = updateTopic.IsAttached.Value;
            }

            if (boardId.HasValue)
            {
                topic.BoardId = boardId.Value;
            }

            await _dbContext.SaveChangesAsync();
        }

        return await _dbContext.Topics
            .TagWith("DM.Forum.UpdatedTopic")
            .Where(t => t.TopicId == updateTopic.TopicId)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid topicId)
    {
        var topic = await _dbContext.Topics.FindAsync(topicId);
        if (topic != null)
        {
            topic.IsRemoved = true;
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task UpdateAttachOrder(IReadOnlyDictionary<Guid, int> topicOrders, CancellationToken ct = default)
    {
        if (topicOrders.Count == 0)
        {
            return;
        }

        var topicIds = topicOrders.Keys.ToArray();
        var topics = await _dbContext.Topics
            .TagWith("DM.Forum.UpdateAttachOrder")
            .Where(t => topicIds.Contains(t.TopicId))
            .ToArrayAsync(ct);

        foreach (var topic in topics)
        {
            if (topicOrders.TryGetValue(topic.TopicId, out var order))
            {
                topic.AttachOrder = order;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
