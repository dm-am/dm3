using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// The board side of its topics: the summary a board keeps of them, and the order
/// of its pinned ones
/// </summary>
internal partial class TopicRepository
{
    /// <inheritdoc />
    public Task RefreshBoardSummary(Guid boardId, CancellationToken ct = default) =>
        // The same recompute the three write paths of TopicRepository.cs run, offered to a
        // caller that changed a topic without going through any of them. It is the
        // one definition of what a board's summary means: a second reading of
        // "the last topic" written next door drifted from this one over the
        // tie-break and overwrote it, and the two then took turns.
        //
        // The transaction is here rather than around the private method because
        // the private one is called from inside transactions that already hold the
        // board row, and a transaction opened inside a transaction is refused.
        RetryableWrite.Run(_dbContext, async cancellation =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            await RefreshBoardTopicSummary(boardId, cancellation);
            await _dbContext.SaveChangesAsync(cancellation);

            await transaction.CommitAsync(cancellation);
        }, ct);

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
        // A recompute is a read of the topics followed by a write of the board, and
        // the board row is what serializes it against the next one: without the lock
        // a delete and a create counted the same board at the same moment, each saw
        // the other's topic missing, and whichever committed last wrote its own
        // number over the truth - a count nothing recomputes until the board is
        // written again. The lock is the first statement of the pair rather than a
        // consequence of the write itself: a wait that begins at the write has
        // already taken its number from a snapshot the wait made stale. Taking it
        // again inside a transaction that holds it (Create) costs nothing.
        await _dbContext.Database.ExecuteSqlRawAsync(
            """SELECT "BoardId" FROM "Boards" WHERE "BoardId" = {0} FOR UPDATE""", [boardId], ct);

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
    public async Task<IReadOnlyList<Guid>> GetAttachedTopicIds(Guid boardId, CancellationToken ct = default) =>
        await _dbContext.Topics
            .TagWith("DM.Forum.AttachedTopicIds")
            .AsNoTracking()
            .Where(t => t.BoardId == boardId && t.IsAttached && !t.IsRemoved)
            .OrderBy(t => t.AttachOrder ?? int.MaxValue)
            .ThenBy(t => t.TopicId)
            .Select(t => t.TopicId)
            .ToArrayAsync(ct);

    /// <inheritdoc />
    public async Task ReplaceAttachOrder(
        Guid boardId, IReadOnlyList<Guid> orderedTopicIds, CancellationToken ct = default)
    {
        // The board is in the predicate and not only in the caller's address:
        // keyed by topic id alone, this write moved whatever ids the body
        // happened to carry, and the moderator of one board could renumber the
        // pinned topics of another.
        var topics = await _dbContext.Topics
            .TagWith("DM.Forum.ReplaceAttachOrder")
            .Where(t => t.BoardId == boardId && t.IsAttached && !t.IsRemoved)
            .ToArrayAsync(ct);

        var positions = new Dictionary<Guid, int>();
        for (var position = 0; position < orderedTopicIds.Count; position++)
        {
            positions[orderedTopicIds[position]] = position;
        }

        foreach (var topic in topics)
        {
            if (positions.TryGetValue(topic.TopicId, out var order))
            {
                topic.AttachOrder = order;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
