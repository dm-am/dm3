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
using DM.Infrastructure.Persistence.RelationalStorage;
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
    public async Task<IReadOnlyDictionary<Guid, PeriodDigest>> GetPeriodDigests(
        IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default)
    {
        if (topicIds.Count == 0) return new Dictionary<Guid, PeriodDigest>();

        var markers = await _dbContext.PeriodDigestTopics
            .TagWith("DM.Forum.TopicPeriodDigests")
            .Where(d => topicIds.Contains(d.TopicId))
            .Select(d => new { d.TopicId, d.Year, d.Month })
            .ToListAsync(ct);

        return markers.ToDictionary(
            m => m.TopicId,
            m => new PeriodDigest { Year = m.Year, Month = m.Month });
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

        // The API host configures EnableRetryOnFailure, and a retrying execution
        // strategy refuses a transaction opened by hand — it has no way to replay
        // one. Everything below therefore runs as a single retriable unit.
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempted = false;
        await strategy.ExecuteAsync(async () =>
        {
            if (attempted)
            {
                // A retry replays this whole block, so anything the failed attempt
                // left tracked has to go: still Added it would insert the topic a
                // second time, already Unchanged it would insert nothing at all.
                _dbContext.ChangeTracker.Clear();
            }

            attempted = true;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            // TopicNumber is allocated as MAX+1 and the board's counters are a
            // read-modify-write, so both are wrong the moment two topics are created
            // in one board at once. The board row is written by this method anyway:
            // locking it first serializes creation per board, which is the smallest
            // scope that makes the number and the counter correct at the same time.
            // The unique index on (BoardId, TopicNumber) stays as the invariant — it
            // is what guarantees a topic URL resolves to one topic no matter who writes.
            await _dbContext.Database.ExecuteSqlRawAsync(
                """SELECT "BoardId" FROM "Boards" WHERE "BoardId" = {0} FOR UPDATE""", [boardId], ct);

            // IgnoreQueryFilters: a removed topic keeps its number. Counted under the
            // soft-delete filter, deleting the newest topic handed its number to the
            // next one, and the deleted topic's permanent URL started resolving to a
            // different topic.
            var maxTopicNumber = await _dbContext.Topics
                .IgnoreQueryFilters()
                .TagWith("DM.Forum.MaxTopicNumber")
                .Where(t => t.BoardId == boardId)
                .Select(t => (int?)t.TopicNumber)
                .MaxAsync(ct) ?? 0;

            _dbContext.Topics.Add(new Entities.Forum.Topic
            {
                TopicId = topicId,
                BoardId = boardId,
                TopicNumber = maxTopicNumber + 1,
                AuthorId = authorId,
                Title = createTopic.Title.Trim(),
                Text = createTopic.Text.Trim(),
                CreatedUtc = now,
                IsRemoved = false,
                IsClosed = false,
                IsAttached = false
            });
            await _dbContext.SaveChangesAsync(ct);

            // The board summary is recomputed here too, rather than incremented, so
            // that creation, deletion and moving share one definition of it. The row
            // lock above already serializes creation per board, so the extra read
            // costs a query and buys the guarantee that the three paths cannot drift.
            await RefreshBoardTopicSummary(boardId, ct);

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

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

            // A move changes the summary of both boards: the one losing the topic
            // and the one gaining it.
            var previousBoardId = topic.BoardId;
            if (boardId.HasValue)
            {
                topic.BoardId = boardId.Value;
            }

            await _dbContext.SaveChangesAsync();

            if (boardId.HasValue && boardId.Value != previousBoardId)
            {
                await RefreshBoardTopicSummary(previousBoardId);
                await RefreshBoardTopicSummary(boardId.Value);
                await _dbContext.SaveChangesAsync();
            }
        }

        return await _dbContext.Topics
            .TagWith("DM.Forum.UpdatedTopic")
            .Where(t => t.TopicId == updateTopic.TopicId)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid topicId, Guid deletedByUserId)
    {
        var topic = await _dbContext.Topics.FindAsync(topicId);
        if (topic != null)
        {
            SoftDelete.Mark(topic, deletedByUserId, _dateTimeProvider.Now);
            await _dbContext.SaveChangesAsync();
            await RefreshBoardTopicSummary(topic.BoardId);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Recompute a board's denormalized topic summary from the topics themselves.
    /// </summary>
    /// <remarks>
    /// Recomputed, not incremented. An increment is only ever as correct as the
    /// number of places that remember to apply it, and the count used to be
    /// raised on creation and adjusted nowhere else: deleting a topic or moving
    /// one to another board left both boards claiming something untrue, with no
    /// way back short of touching the database by hand. A recompute is the single
    /// definition of what the summary means, and it heals a row that is already
    /// wrong instead of carrying the error forward.
    ///
    /// The soft-delete filter applies, so a removed topic drops out of both the
    /// count and the "last topic" fields, which is what a reader expects to see.
    /// </remarks>
    private async Task RefreshBoardTopicSummary(Guid boardId, CancellationToken ct = default)
    {
        var board = await _dbContext.Boards.FindAsync([boardId], ct);
        if (board == null)
        {
            return;
        }

        var summary = await _dbContext.Topics
            .TagWith("DM.Forum.BoardTopicSummary")
            .Where(t => t.BoardId == boardId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Last = g.OrderByDescending(t => t.CreatedUtc)
                    .ThenByDescending(t => t.TopicNumber)
                    .Select(t => new
                    {
                        t.TopicId,
                        t.TopicNumber,
                        t.Title,
                        t.AuthorId,
                        t.CreatedUtc
                    })
                    .First()
            })
            .FirstOrDefaultAsync(ct);

        board.TopicsCount = summary?.Count ?? 0;
        board.LastTopicId = summary?.Last.TopicId;
        board.LastTopicNumber = summary?.Last.TopicNumber;
        board.LastTopicTitle = summary?.Last.Title;
        board.LastTopicAuthorId = summary?.Last.AuthorId;
        board.LastTopicCreatedUtc = summary?.Last.CreatedUtc;
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
