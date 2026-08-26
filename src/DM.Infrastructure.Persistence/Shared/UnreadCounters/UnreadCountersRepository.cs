using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace DM.Infrastructure.Persistence.Shared.UnreadCounters;

using IUnreadCountersRepository = DM.Domain.Core.UnreadCounters.IUnreadCountersRepository;

/// <inheritdoc cref="IUnreadCountersRepository" />
/// <remarks>
/// The primary key of the table is the triple every write addresses, and every
/// write that can meet an existing marker is INSERT ... ON CONFLICT: the server
/// settles encountering upserts atomically, so the client-side duplicate-key
/// retry this repository used to carry is gone. The IsRemoved predicates are
/// spelled out per read — the entity opts out of the global soft-delete filter,
/// because the upsert paths deliberately revive tombstones while the flush
/// paths must ignore them.
/// </remarks>
internal class UnreadCountersRepository : IUnreadCountersRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UnreadCountersRepository> _logger;

    /// <inheritdoc />
    public UnreadCountersRepository(DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<UnreadCountersRepository> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task CreateMarkerAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds)
    {
        // Upsert, not insert: a user can be counted in for an entity they already
        // have a marker in — removed from a group chat once and added back — and
        // the primary key refuses a second row. Resetting gives what the second
        // insert used to give anyway: the aggregate reads take Min(Counter) and
        // answered with the fresh zero. The reset also revives a tombstone on
        // purpose — the marker belongs to a participant added back.
        var rightNow = _dateTimeProvider.Now.UtcDateTime;
        var markers = userIds
            .Distinct()
            .Select(id => new Entities.Shared.UnreadCounter
            {
                UserId = id,
                EntityId = entityId,
                ParentId = id,
                EntryType = entryType,
                LastReadUtc = rightNow,
                Counter = 0
            })
            .ToArray();

        // Nobody to count for is not an error: the callers happen to guard it,
        // nothing makes them.
        return markers.Length == 0
            ? Task.CompletedTask
            : UpsertMarkersAsync(markers);
    }

    /// <inheritdoc />
    public Task CreateMarkerAsync(Guid entityId, Guid parentId, UnreadEntryType entryType)
    {
        // Same reason as above: the anonymous marker is addressed by the primary
        // key, so a repeated create resets it instead of failing on it.
        return UpsertMarkersAsync(new[]
        {
            new Entities.Shared.UnreadCounter
            {
                UserId = Guid.Empty,
                EntityId = entityId,
                ParentId = parentId,
                EntryType = entryType,
                LastReadUtc = _dateTimeProvider.Now.UtcDateTime,
                Counter = 0
            }
        });
    }

    /// <inheritdoc />
    public Task CreateMarkerAsync(Guid entityId, UnreadEntryType entryType) => CreateMarkerAsync(entityId, entityId, entryType);

    /// <inheritdoc />
    public Task IncrementAsync(Guid entityId, UnreadEntryType entryType) =>
        CountedAsync("increment", entityId, entryType, () => _dbContext.UnreadCounters
            .Where(c => c.EntityId == entityId && c.EntryType == entryType)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Counter, c => c.Counter + 1)));

    /// <inheritdoc />
    public Task IncrementExcludingAsync(Guid entityId, UnreadEntryType entryType, Guid excludeUserId) =>
        CountedAsync("increment_excluding", entityId, entryType, () => _dbContext.UnreadCounters
            .Where(c => c.EntityId == entityId && c.EntryType == entryType && c.UserId != excludeUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Counter, c => c.Counter + 1)));

    /// <inheritdoc />
    public Task DecrementAsync(Guid entityId, UnreadEntryType entryType, DateTimeOffset createDate) =>
        CountedAsync("decrement", entityId, entryType, () => _dbContext.UnreadCounters
            .Where(c => c.EntityId == entityId && c.EntryType == entryType &&
                        c.LastReadUtc < createDate.UtcDateTime)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Counter, c => c.Counter - 1)));

    /// <summary>
    /// Runs one adjustment of an existing marker and keeps its failure to itself.
    /// </summary>
    /// <remarks>
    /// Every caller of these three reaches them after its own write has been
    /// committed, and the increment is not part of that transaction. So an
    /// exception here does not undo anything - it travels up through a service
    /// that has already committed, and the caller is answered with a failure for
    /// work that was in fact done. The reader then sees the post they wrote,
    /// plus an error saying it was not written, and a retry writes it twice.
    ///
    /// Losing the count is the smaller loss, and it is bounded: the badge is a
    /// derived number, one "mark as read" resets it, and nothing else is built on
    /// it. That trade only holds while somebody can see it happening, which is
    /// what the counter and the entry are for.
    ///
    /// The three creating and removing writes are deliberately not routed through
    /// this. A marker that was never created is not a wrong number, it is an
    /// entity nobody is counting at all, and the reservation that orders those
    /// writes before the relational row exists precisely so that this failure
    /// arrives while the row can still be rolled back.
    /// </remarks>
    private async Task CountedAsync(
        string operation, Guid entityId, UnreadEntryType entryType, Func<Task> write)
    {
        try
        {
            await write();
        }
        catch (Exception exception)
        {
            StorageMetrics.WriteLost.Add(1,
                StorageMetrics.Store(StorageMetrics.RelationalStore),
                StorageMetrics.Operation($"unread_counters.{operation}"),
                new KeyValuePair<string, object?>("reason", exception.GetType().Name));
            _logger.LogWarning(exception,
                "Unread counter {Operation} lost for {EntryType} {EntityId}: the work it " +
                "belongs to is already committed",
                operation, entryType, entityId);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid entityId, UnreadEntryType entryType)
    {
        // Stamped, not only flagged. The tombstone keeps a deleted entity from
        // coming back through "mark as read" — both flush paths look for a live
        // marker to copy the parent from — and that job is over in minutes,
        // while the row used to stay forever. The moment of removal is what the
        // retention sweep reads, so the tombstone now expires on its own.
        return _dbContext.UnreadCounters
            .Where(c => c.EntityId == entityId && c.EntryType == entryType)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsRemoved, true)
                .SetProperty(c => c.RemovedUtc, _dateTimeProvider.Now.UtcDateTime));
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds)
    {
        var readers = userIds.Distinct().ToArray();

        // Nobody to forget is not an error, the same way nobody to count in is
        // not one for CreateMarkerAsync above.
        return readers.Length == 0
            ? Task.CompletedTask
            : _dbContext.UnreadCounters
                .Where(c => readers.Contains(c.UserId) && c.EntityId == entityId && c.EntryType == entryType)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsRemoved, true)
                    .SetProperty(c => c.RemovedUtc, _dateTimeProvider.Now.UtcDateTime));
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, int>> SelectByParentsAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] parentIds)
    {
        var counters = (await AggregateByParents(userId, entryType, parentIds)
                .Select(g => new { ParentId = g.Key, Counter = g.Count(e => e.Counter > 0) })
                .ToListAsync())
            .ToDictionary(c => c.ParentId, c => c.Counter);
        return parentIds.ToDictionary(id => id, id => counters.TryGetValue(id, out var counter) ? counter : 0);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, int>> SelectTotalUnreadByParentsAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] parentIds)
    {
        var counters = (await AggregateByParents(userId, entryType, parentIds)
                .Select(g => new { ParentId = g.Key, Counter = g.Sum(e => e.Counter) })
                .ToListAsync())
            .ToDictionary(c => c.ParentId, c => c.Counter);
        return parentIds.ToDictionary(id => id, id => counters.TryGetValue(id, out var counter) ? counter : 0);
    }

    /// <summary>
    /// One value per entity under the asked-for parents, grouped back under the
    /// parent for the caller's aggregate. Min over the user's own marker and
    /// the anonymous one keeps the semantics the aggregates always had: the
    /// reader's own state wins where both exist.
    /// </summary>
    private IQueryable<IGrouping<Guid, EntityAggregate>> AggregateByParents(
        Guid userId, UnreadEntryType entryType, Guid[] parentIds)
    {
        var userIds = new[] { userId, Guid.Empty }.Distinct().ToArray();
        // Grouped by the pair rather than by the entity alone: the rows of one
        // entity in this filtered set share their parent (a topic is parented
        // by its board for the reader and for the anonymous marker alike), and
        // the server has no aggregate to pick a uuid out of a group with.
        return _dbContext.UnreadCounters
            .TagWith("DM.UnreadCounters.ByParents")
            .Where(c => userIds.Contains(c.UserId) &&
                        parentIds.Contains(c.ParentId) &&
                        c.EntryType == entryType &&
                        !c.IsRemoved)
            .GroupBy(c => new { c.ParentId, c.EntityId })
            .Select(g => new EntityAggregate
            {
                ParentId = g.Key.ParentId,
                Counter = g.Min(c => c.Counter)
            })
            .GroupBy(e => e.ParentId);
    }

    private sealed class EntityAggregate
    {
        public Guid ParentId { get; init; }
        public int Counter { get; init; }
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, int>> SelectByEntitiesAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] entityIds)
    {
        var userIds = new[] { userId, Guid.Empty }.Distinct().ToArray();
        var counters = (await _dbContext.UnreadCounters
                .TagWith("DM.UnreadCounters.ByEntities")
                .Where(c => userIds.Contains(c.UserId) &&
                            entityIds.Contains(c.EntityId) &&
                            c.EntryType == entryType &&
                            !c.IsRemoved)
                .GroupBy(c => c.EntityId)
                .Select(g => new { EntityId = g.Key, Counter = g.Min(c => c.Counter) })
                .ToListAsync())
            .ToDictionary(c => c.EntityId, c => c.Counter);
        return entityIds.ToDictionary(id => id, id => counters.TryGetValue(id, out var counter) ? counter : 0);
    }

    /// <inheritdoc />
    public async Task FlushAsync(Guid userId, UnreadEntryType entryType, Guid entityId)
    {
        // The reader's own marker first, and when it exists nothing else is
        // consulted: its ParentId is already the right one and must survive
        // untouched.
        //
        // Borrowing a parent from whichever marker the entity happened to return
        // is only sound where every reader of an entity shares one parent — a
        // topic parented by its board, a room by its game. A conversation is the
        // exception: its marker is parented by the reader themselves, which is
        // what makes "all my conversations" answerable at all. Copying a
        // neighbour's parent there stamped one participant's marker with another
        // participant's identifier, and the conversation then matched neither of
        // them in a parent-scoped read — it did not move to the wrong total, it
        // dropped out of every total.
        var flushedOwn = await _dbContext.UnreadCounters
            .Where(c => c.UserId == userId && c.EntityId == entityId && c.EntryType == entryType &&
                        !c.IsRemoved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Counter, 0)
                .SetProperty(c => c.LastReadUtc, _dateTimeProvider.Now.UtcDateTime));
        if (flushedOwn > 0)
        {
            return;
        }

        // No marker of one's own: the parent has to come from somewhere, and a
        // neighbour is the only place it exists. When there is none either, the
        // entity was never counted for anyone — there is nothing to mark as read,
        // and writing a marker with an invented ParentId would hide it from
        // FlushAllAsync, which filters by exactly that field.
        // The IsRemoved predicate is spelled out on purpose: a deleted entity
        // still finds its own tombstoned counter here, and the upsert below
        // would write a live row back — the entity returns to the sidebar with
        // a fresh marker.
        var counter = await _dbContext.UnreadCounters
            .TagWith("DM.UnreadCounters.FlushDonor")
            .Where(c => c.EntityId == entityId && c.EntryType == entryType && !c.IsRemoved)
            .FirstOrDefaultAsync();
        if (counter == null)
        {
            return;
        }

        await UpsertMarkersAsync(new[]
        {
            new Entities.Shared.UnreadCounter
            {
                UserId = userId,
                EntityId = entityId,
                ParentId = counter.ParentId,
                EntryType = entryType,
                LastReadUtc = _dateTimeProvider.Now.UtcDateTime,
                Counter = 0
            }
        });
    }

    /// <inheritdoc />
    public async Task FlushAllAsync(Guid userId, UnreadEntryType entryType, Guid parentId)
    {
        // Same reason as FlushAsync: a deleted entity must not come back through
        // "mark everything as read".
        var entityIds = await _dbContext.UnreadCounters
            .TagWith("DM.UnreadCounters.FlushAll")
            .Where(c => c.ParentId == parentId && c.EntryType == entryType && !c.IsRemoved)
            .Select(c => c.EntityId)
            .Distinct()
            .ToListAsync();

        var rightNow = _dateTimeProvider.Now.UtcDateTime;
        var markers = entityIds
            .Select(id => new Entities.Shared.UnreadCounter
            {
                UserId = userId,
                EntityId = id,
                ParentId = parentId,
                EntryType = entryType,
                LastReadUtc = rightNow,
                Counter = 0
            })
            .ToArray();

        // Nothing unread under this parent is the ordinary state of a user who
        // reads everything.
        if (markers.Length == 0)
        {
            return;
        }

        await UpsertMarkersAsync(markers);
    }

    /// <inheritdoc />
    public async Task ChangeParentAsync(Guid parentId, UnreadEntryType entryType, Guid newParentId)
    {
        await _dbContext.UnreadCounters
            .Where(c => c.ParentId == parentId && c.EntryType == entryType)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentId, newParentId));
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastReadTimeAsync(Guid userId, Guid entityId, UnreadEntryType entryType)
    {
        // At most one row: the triple is the primary key.
        var counter = await _dbContext.UnreadCounters
            .TagWith("DM.UnreadCounters.LastRead")
            .Where(c => c.UserId == userId && c.EntityId == entityId && c.EntryType == entryType &&
                        !c.IsRemoved)
            .FirstOrDefaultAsync();

        return counter?.LastReadUtc;
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, DateTime>> GetLastReadTimesAsync(Guid userId, UnreadEntryType entryType, params Guid[] entityIds)
    {
        var counters = await _dbContext.UnreadCounters
            .TagWith("DM.UnreadCounters.LastReads")
            .Where(c => c.UserId == userId &&
                        entityIds.Contains(c.EntityId) &&
                        c.EntryType == entryType &&
                        !c.IsRemoved)
            .ToListAsync();

        return counters.ToDictionary(c => c.EntityId, c => c.LastReadUtc);
    }

    /// <summary>
    /// The one spelling of "this marker starts over": INSERT ... ON CONFLICT on
    /// the primary key, resetting the counter, the read moment and the
    /// tombstone. Atomic on the server against a concurrent upsert of the same
    /// key — two tabs, a double click on "mark as read", a retried request over
    /// a mobile network — so no client retry exists to get wrong.
    /// </summary>
    private async Task UpsertMarkersAsync(IReadOnlyList<Entities.Shared.UnreadCounter> markers)
    {
        var sql = new StringBuilder(
            """
            INSERT INTO "UnreadCounters" ("UserId", "EntityId", "EntryType", "ParentId", "LastReadUtc", "Counter", "IsRemoved", "RemovedUtc")
            VALUES
            """);
        var parameters = new List<NpgsqlParameter>(markers.Count * 5);

        for (var i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            var p = i * 5;
            sql.Append(i == 0 ? " " : ", ");
            sql.Append($"(@p{p}, @p{p + 1}, @p{p + 2}, @p{p + 3}, @p{p + 4}, 0, FALSE, NULL)");
            parameters.Add(new NpgsqlParameter($"p{p}", marker.UserId));
            parameters.Add(new NpgsqlParameter($"p{p + 1}", marker.EntityId));
            parameters.Add(new NpgsqlParameter($"p{p + 2}", (int)marker.EntryType));
            parameters.Add(new NpgsqlParameter($"p{p + 3}", marker.ParentId));
            parameters.Add(new NpgsqlParameter($"p{p + 4}", NpgsqlDbType.TimestampTz) { Value = marker.LastReadUtc });
        }

        sql.Append(
            """

            ON CONFLICT ("UserId", "EntityId", "EntryType") DO UPDATE SET
                "ParentId" = EXCLUDED."ParentId",
                "LastReadUtc" = EXCLUDED."LastReadUtc",
                "Counter" = 0,
                "IsRemoved" = FALSE,
                "RemovedUtc" = NULL
            """);

        await _dbContext.Database.ExecuteSqlRawAsync(sql.ToString(), parameters);
    }
}
