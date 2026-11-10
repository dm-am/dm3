using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Likes;
using Microsoft.EntityFrameworkCore;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

internal partial class TopicRepository : ITopicRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TopicRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<Topic?> Get(Guid topicId, CancellationToken ct = default) =>
        ReadOne(_dbContext.Topics.TagWith("DM.Forum.Topic").Where(t => t.TopicId == topicId), ct);

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

    // These two probes ignore the soft-delete filter, so a deleted topic answers 410, not 404.
    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<Topic?> GetByBoardAndNumber(Guid boardId, int topicNumber, CancellationToken ct = default) =>
        ReadOne(_dbContext.Topics
            .TagWith("DM.Forum.TopicByBoardAndNumber")
            .Where(t => t.BoardId == boardId && t.TopicNumber == topicNumber), ct);

    /// <inheritdoc />
    public Task<Topic?> GetBestUserTopic(Guid authorId, CancellationToken ct = default) =>
        ReadOne(_dbContext.Topics
            .TagWith("DM.Forum.GetBestUserTopic")
            .Where(t => t.AuthorId == authorId)
            .OrderByDescending(t => _dbContext.Likes.Count(l =>
                l.EntityId == t.TopicId &&
                l.EntityType == LikeEntityType.Topic))
            // Newer first, then the identifier, so ties still answer one topic.
            .ThenByDescending(t => t.CreatedUtc)
            .ThenBy(t => t.TopicId), ct);

    private async Task<Topic?> ReadOne(IQueryable<DbTopic> topics, CancellationToken ct)
    {
        var topic = await topics
            .AsNoTracking()
            .ProjectToTopic()
            .FirstOrDefaultAsync(ct);

        await FillCounts(topic, ct);
        return topic;
    }

    /// <summary>
    /// Backfill <see cref="Topic.TotalCommentsCount"/>,
    /// <see cref="Topic.LikesCount"/> and <see cref="Topic.Likes"/> for a single
    /// topic, the values the list path fills in batches: the projection leaves them
    /// out to avoid per-row correlated subqueries.
    /// </summary>
    private async Task FillCounts(Topic? topic, CancellationToken ct)
    {
        if (topic == null)
        {
            return;
        }

        await FillListCounts([topic], ct);
        await FillLikes([topic], ct);
    }

    /// <summary>
    /// Backfill <see cref="Topic.Likes"/> - who liked each topic of a page.
    /// </summary>
    /// <remarks>
    /// The projection cannot carry them (see <see cref="LikeBackfill" />), and
    /// without this every topic answered with an empty list: the reader who had
    /// liked a topic saw an empty heart after a reload, and the list of those who
    /// liked it was empty however many had.
    /// </remarks>
    private Task FillLikes(IReadOnlyCollection<Topic> topics, CancellationToken ct) =>
        LikeBackfill.Fill(_dbContext, topics, LikeEntityType.Topic,
            t => t.Id, (t, likes) => t.Likes = likes,
            "DM.Forum.TopicLikes", "DM.Forum.TopicLikers", ct);

    /// <inheritdoc />
    public async Task<Topic> Create(CreateTopicEntity createTopic, Guid authorId, Guid boardId, CancellationToken ct = default)
    {
        var topicId = createTopic.TopicId;
        var now = _dateTimeProvider.Now;

        // The API host configures EnableRetryOnFailure, and a retrying execution
        // strategy refuses a transaction opened by hand — it has no way to replay
        // one. Everything below therefore runs as a single retriable unit.
        await RetryableWrite.Run(_dbContext, async () =>
        {
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

            _dbContext.Topics.Add(new DbTopic
            {
                TopicId = topicId,
                BoardId = boardId,
                TopicNumber = await NextTopicNumber(boardId, ct),
                AuthorId = authorId,
                Title = createTopic.Title,
                Text = AuthoredText.AsTyped(createTopic.Text),
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
            .ProjectToTopic()
            .FirstAsync(ct);
    }

    /// <summary>
    /// The number the next topic of this board takes.
    /// </summary>
    /// <remarks>
    /// Callers hold the board row, which is what makes MAX + 1 an allocation
    /// rather than a guess: two of these running at once read the same maximum.
    ///
    /// IgnoreQueryFilters: a removed topic keeps its number. Counted under the
    /// soft-delete filter, deleting the newest topic handed its number to the next
    /// one, and the deleted topic's permanent URL started resolving to a different
    /// topic.
    /// </remarks>
    private async Task<int> NextTopicNumber(Guid boardId, CancellationToken ct = default)
    {
        var highest = await _dbContext.Topics
            .IgnoreQueryFilters()
            .TagWith("DM.Forum.MaxTopicNumber")
            .Where(t => t.BoardId == boardId)
            .Select(t => (int?)t.TopicNumber)
            .MaxAsync(ct);

        return (highest ?? 0) + 1;
    }

    /// <inheritdoc />
    public async Task<TopicUpdateResult> Update(UpdateTopicEntity updateTopic, Guid? boardId = null)
    {
        // Both writes or neither. The second one recomputes the denormalised summary
        // of the boards a move touches, and separately a refusal between them left
        // two boards counting a topic that is no longer theirs - a number nothing
        // recomputes until the next topic moves.
        //
        // Through the strategy because the API host configures EnableRetryOnFailure and
        // a retrying strategy refuses a transaction opened by hand. The read is
        // inside the block: cleared out of the tracker by a retry, an entity read
        // outside it would take every edit with it.
        var changed = false;
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // Both boards, in a fixed order, before the topic row is written. A
            // move allocates the topic's number in the board it joins and
            // recomputes the summary of both, and each of those is a read the
            // wait has to come before: taken after the write, the number came
            // from a snapshot the wait had already made stale, and the unique
            // index on (BoardId, TopicNumber) refused it.
            if (boardId.HasValue)
            {
                await LockBoardsForMove(updateTopic.TopicId, boardId.Value);
            }

            var topic = await _dbContext.Topics.FindAsync(updateTopic.TopicId);
            if (topic != null)
            {
                ApplyEdit(topic, updateTopic);

                // A move changes the summary of both boards: the one losing the topic
                // and the one gaining it.
                var previousBoardId = topic.BoardId;
                if (boardId.HasValue && boardId.Value != previousBoardId)
                {
                    await MoveToBoard(topic, boardId.Value);
                }

                changed = RecordEdit(topic, updateTopic.EditorUserId);

                await _dbContext.SaveChangesAsync();

                if (boardId.HasValue && boardId.Value != previousBoardId)
                {
                    // Both rows are already held: the statement at the top of the
                    // transaction took them, in the order the identifiers sort in.
                    await RefreshBoardTopicSummary(previousBoardId);
                    await RefreshBoardTopicSummary(boardId.Value);
                    await _dbContext.SaveChangesAsync();
                }
            }

            await transaction.CommitAsync();
        });

        var updated = await _dbContext.Topics
            .TagWith("DM.Forum.UpdatedTopic")
            .Where(t => t.TopicId == updateTopic.TopicId)
            .ProjectToTopic()
            .FirstAsync();

        // The client puts the answer to an edit in place of the topic it shows,
        // so the answer carries what a read carries: without it an edit emptied
        // the heart of the reader who had liked the topic and zeroed its counts.
        await FillCounts(updated, CancellationToken.None);

        return new TopicUpdateResult(updated, changed);
    }

    private async Task LockBoardsForMove(Guid topicId, Guid boardId)
    {
        // Ordering by identifier is arbitrary, which is all a lock order has
        // to be as long as everyone shares it: a move is the only write here
        // that holds two board rows at once, so two moves in opposite
        // directions are the pair that would otherwise take them in opposite
        // orders and deadlock. The board the topic is leaving is named by a
        // subquery rather than by a read of its own, so that nothing this
        // transaction decides on happens before the lock.
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            SELECT "BoardId" FROM "Boards"
            WHERE "BoardId" IN ((SELECT "BoardId" FROM "Topics" WHERE "TopicId" = {0}), {1})
            ORDER BY "BoardId"
            FOR UPDATE
            """,
            topicId, boardId);
    }

    private static void ApplyEdit(DbTopic topic, UpdateTopicEntity updateTopic)
    {
        if (!string.IsNullOrEmpty(updateTopic.Title))
        {
            topic.Title = updateTopic.Title;
        }

        // null = don't update (the UpdateTopic contract); an empty
        // string is a deliberate clear — topic text is optional.
        if (updateTopic.Text != null)
        {
            topic.Text = AuthoredText.AsTyped(updateTopic.Text);
        }

        if (updateTopic.IsClosed.HasValue)
        {
            topic.IsClosed = updateTopic.IsClosed.Value;
        }

        if (updateTopic.ClosedByAuthor.HasValue)
        {
            topic.ClosedByAuthor = updateTopic.ClosedByAuthor.Value;
        }

        if (updateTopic.IsAttached.HasValue)
        {
            topic.IsAttached = updateTopic.IsAttached.Value;
        }
    }

    private async Task MoveToBoard(DbTopic topic, Guid boardId)
    {
        topic.BoardId = boardId;

        // The number is per board, and the topic kept the one it had:
        // moved into a board that already had a topic under that
        // number, the write was refused by the unique index on
        // (BoardId, TopicNumber) and the move failed outright. It is
        // allocated the way creation allocates one, under the same
        // board lock and over the same set, so a move and a creation
        // racing for the next number cannot both take it.
        //
        // The old address stops resolving, which is the price of the
        // number meaning "the Nth topic of this board": the topic
        // keeps its identifier and every link built on that still
        // opens it.
        topic.TopicNumber = await NextTopicNumber(boardId);
    }

    private bool RecordEdit(DbTopic topic, Guid editorUserId)
    {
        // The tracker is the one fact about whether this request changed anything:
        // a request that round-trips the values the topic already holds leaves it
        // Unchanged. Both consequences hang off it — no row in the edit history,
        // and no ChangedTopic announcement, since a notification whose actor is
        // read back from that history would otherwise name the previous editor.
        var changed = _dbContext.Entry(topic).State == EntityState.Modified;

        // The topic row keeps its author and no editor, so the history is the
        // only record of who changed it.
        if (editorUserId != Guid.Empty && changed)
        {
            _dbContext.TopicEdits.Add(new Entities.Forum.TopicEdit
            {
                TopicEditId = _guidFactory.Create(),
                TopicId = topic.TopicId,
                EditorUserId = editorUserId,
                EditedUtc = _dateTimeProvider.Now
            });
        }

        return changed;
    }

    /// <inheritdoc />
    public async Task Delete(Guid topicId, Guid deletedByUserId)
    {
        // Both writes or neither: separately, a refusal between them left the board
        // counting a topic that is gone. The read is inside the block for the same
        // reason as in Update above.
        //
        // The subscriptions to the topic go in the same transaction. Nothing brings
        // a deleted topic back, so a subscription that outlived it could never
        // deliver anything: it only stood in the reader's list as a nameless row
        // ("Топик n/a") that was theirs to clean up.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var topic = await _dbContext.Topics.FindAsync(topicId);
            if (topic != null)
            {
                SoftDelete.Mark(topic, deletedByUserId, _dateTimeProvider.Now);
                await _dbContext.SaveChangesAsync();
                await RefreshBoardTopicSummary(topic.BoardId);
                await _dbContext.SaveChangesAsync();

                await _dbContext.Subscriptions
                    .Where(s => s.TargetType == SubscriptionTargetType.Topic && s.TargetId == topicId)
                    .ExecuteDeleteAsync();
            }

            await transaction.CommitAsync();
        });
    }
}
