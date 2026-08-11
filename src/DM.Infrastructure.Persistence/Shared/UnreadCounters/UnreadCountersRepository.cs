using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Driver;

namespace DM.Infrastructure.Persistence.Shared.UnreadCounters;

using IUnreadCountersRepository = DM.Domain.Core.UnreadCounters.IUnreadCountersRepository;

/// <inheritdoc cref="IUnreadCountersRepository" />
internal class UnreadCountersRepository : MongoCollectionRepository<UnreadCounter>, IUnreadCountersRepository
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public UnreadCountersRepository(DmMongoClient client,
        IDateTimeProvider dateTimeProvider) : base(client)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task CreateAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds)
    {
        // Upsert, not insert: a user can be counted in for an entity they already
        // have a marker in — removed from a group chat once and added back — and
        // the unique index refuses the second document. Replacing gives what the
        // second insert used to give anyway: the aggregate reads take Min(Counter)
        // and answered with the fresh zero.
        var rightNow = _dateTimeProvider.Now.UtcDateTime;
        var markers = userIds
            .Distinct()
            .Select(id => Upsert(new UnreadCounter
            {
                UserId = id,
                EntityId = entityId,
                ParentId = id,
                EntryType = entryType,
                LastReadUtc = rightNow,
                Counter = 0
            }))
            .ToArray();

        // A bulk write refuses an empty batch, and nobody to count for is not an
        // error: the callers happen to guard it, nothing makes them.
        return markers.Length == 0
            ? Task.CompletedTask
            : UpsertAsync(() => Collection.BulkWriteAsync(markers));
    }

    /// <inheritdoc />
    public Task CreateAsync(Guid entityId, Guid parentId, UnreadEntryType entryType)
    {
        // Same reason as above: the anonymous marker is addressed by the key the
        // unique index enforces, so a repeated create resets it instead of failing
        // on it.
        return UpsertAsync(() => Collection.ReplaceOneAsync(
            Key(Guid.Empty, entityId, entryType),
            new UnreadCounter
            {
                UserId = Guid.Empty,
                EntityId = entityId,
                ParentId = parentId,
                EntryType = entryType,
                LastReadUtc = _dateTimeProvider.Now.UtcDateTime,
                Counter = 0
            },
            new ReplaceOptions { IsUpsert = true }));
    }

    /// <inheritdoc />
    public Task CreateAsync(Guid entityId, UnreadEntryType entryType) => CreateAsync(entityId, entityId, entryType);

    /// <inheritdoc />
    public Task IncrementAsync(Guid entityId, UnreadEntryType entryType)
    {
        return Collection.UpdateManyAsync(
            Filter.Eq(c => c.EntityId, entityId) &
            Filter.Eq(c => c.EntryType, entryType),
            Update.Inc(c => c.Counter, 1));
    }

    /// <inheritdoc />
    public Task IncrementExcludingAsync(Guid entityId, UnreadEntryType entryType, Guid excludeUserId)
    {
        return Collection.UpdateManyAsync(
            Filter.Eq(c => c.EntityId, entityId) &
            Filter.Eq(c => c.EntryType, entryType) &
            Filter.Ne(c => c.UserId, excludeUserId),
            Update.Inc(c => c.Counter, 1));
    }

    /// <inheritdoc />
    public Task DecrementAsync(Guid entityId, UnreadEntryType entryType, DateTimeOffset createDate)
    {
        return Collection.UpdateManyAsync(Filter.Eq(c => c.EntityId, entityId) &
                                          Filter.Eq(c => c.EntryType, entryType) &
                                          Filter.Lt(c => c.LastReadUtc, createDate.UtcDateTime),
            Update.Inc(c => c.Counter, -1));
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid entityId, UnreadEntryType entryType)
    {
        // Stamped, not only flagged. The tombstone keeps a deleted entity from
        // coming back through "mark as read" — both flush paths look for a live
        // marker to copy the parent from — and that job is over in minutes, while
        // the document used to stay forever: every topic, room and conversation
        // ever deleted kept one marker per user who had opened it, and nothing
        // collected them. The moment of removal is what the collection's TTL index
        // reads, so the tombstone now expires on its own.
        return Collection.UpdateManyAsync(
            Filter.Eq(c => c.EntityId, entityId) &
            Filter.Eq(c => c.EntryType, entryType),
            Tombstone());
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds)
    {
        var readers = userIds.Distinct().ToArray();

        // Nobody to forget is not an error, the same way nobody to count in is
        // not one for CreateAsync above.
        return readers.Length == 0
            ? Task.CompletedTask
            : Collection.UpdateManyAsync(
                Filter.In(c => c.UserId, readers) &
                Filter.Eq(c => c.EntityId, entityId) &
                Filter.Eq(c => c.EntryType, entryType),
                Tombstone());
    }

    /// <summary>
    /// What a marker that no longer counts anything looks like.
    /// </summary>
    /// <remarks>
    /// One spelling for both removals: the stamp is what the collection's expiry
    /// index reads, so a second copy of this that forgot it would leave the
    /// document behind forever.
    /// </remarks>
    private UpdateDefinition<UnreadCounter> Tombstone() => Update
        .Set(c => c.IsRemoved, true)
        .Set(c => c.RemovedUtc, _dateTimeProvider.Now.UtcDateTime);

    /// <inheritdoc />
    public async Task<IDictionary<Guid, int>> SelectByParentsAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] parentIds)
    {
        var userIds = new[] { userId, Guid.Empty }.Distinct();
        var counters = (await Collection.Aggregate()
                .Match(
                    Filter.In(c => c.UserId, userIds) &
                    Filter.In(c => c.ParentId, parentIds) &
                    Filter.Eq(c => c.EntryType, entryType) &
                    Filter.Eq(c => c.IsRemoved, false))
                .Group(c => c.EntityId,
                    g => new UnreadCounter
                    {
                        EntityId = g.First().EntityId,
                        ParentId = g.First().ParentId,
                        Counter = g.Min(c => c.Counter)
                    })
                .Group(c => c.ParentId,
                    g => new UnreadCounter
                    {
                        EntityId = g.First().ParentId,
                        Counter = g.Sum(c => c.Counter > 0 ? 1 : 0)
                    })
                .ToListAsync())
            .ToDictionary(c => c.EntityId, c => c.Counter);
        return parentIds.ToDictionary(id => id, id => counters.TryGetValue(id, out var counter) ? counter : 0);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, int>> SelectTotalUnreadByParentsAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] parentIds)
    {
        var userIds = new[] { userId, Guid.Empty }.Distinct();
        var counters = (await Collection.Aggregate()
                .Match(
                    Filter.In(c => c.UserId, userIds) &
                    Filter.In(c => c.ParentId, parentIds) &
                    Filter.Eq(c => c.EntryType, entryType) &
                    Filter.Eq(c => c.IsRemoved, false))
                .Group(c => c.EntityId,
                    g => new UnreadCounter
                    {
                        EntityId = g.First().EntityId,
                        ParentId = g.First().ParentId,
                        Counter = g.Min(c => c.Counter)
                    })
                .Group(c => c.ParentId,
                    g => new UnreadCounter
                    {
                        EntityId = g.First().ParentId,
                        Counter = g.Sum(c => c.Counter) // SUM instead of COUNT
                    })
                .ToListAsync())
            .ToDictionary(c => c.EntityId, c => c.Counter);
        return parentIds.ToDictionary(id => id, id => counters.TryGetValue(id, out var counter) ? counter : 0);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, int>> SelectByEntitiesAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] entityIds)
    {
        var userIds = new[] { userId, Guid.Empty }.Distinct();
        var counters = (await Collection.Aggregate()
                .Match(
                    Filter.In(c => c.UserId, userIds) &
                    Filter.In(c => c.EntityId, entityIds) &
                    Filter.Eq(c => c.EntryType, entryType) &
                    Filter.Eq(c => c.IsRemoved, false))
                .Group(c => c.EntityId,
                    g => new UnreadCounter
                    {
                        EntityId = g.First().EntityId,
                        Counter = g.Min(c => c.Counter)
                    })
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
        var own = await Collection.Find(
                Key(userId, entityId, entryType) &
                Filter.Eq(c => c.IsRemoved, false))
            .FirstOrDefaultAsync();

        if (own != null)
        {
            await Collection.UpdateOneAsync(
                Key(userId, entityId, entryType) & Filter.Eq(c => c.IsRemoved, false),
                Update
                    .Set(c => c.Counter, 0)
                    .Set(c => c.LastReadUtc, _dateTimeProvider.Now.UtcDateTime));
            return;
        }

        // No marker of one's own: the parent has to come from somewhere, and a
        // neighbour is the only place it exists. When there is none either, the
        // entity was never counted for anyone — there is nothing to mark as read,
        // and writing a marker with an invented ParentId would hide it from
        // FlushAllAsync, which filters by exactly that field.
        // Mongo has no global soft-delete filter of its own, so IsRemoved has to be
        // spelled out. Without it a deleted entity still finds its own tombstoned
        // counter here, and the upsert below writes a live row back — the entity
        // returns to the sidebar with a fresh marker.
        var counter = await Collection.Find(
                Filter.Eq(c => c.EntityId, entityId) &
                Filter.Eq(c => c.EntryType, entryType) &
                Filter.Eq(c => c.IsRemoved, false))
            .FirstOrDefaultAsync();
        if (counter == null)
        {
            return;
        }

        await UpsertAsync(() => Collection
            .ReplaceOneAsync(
                Key(userId, entityId, entryType),
                new UnreadCounter
                {
                    UserId = userId,
                    EntityId = entityId,
                    ParentId = counter.ParentId,
                    EntryType = entryType,
                    LastReadUtc = _dateTimeProvider.Now.UtcDateTime,
                    Counter = 0
                },
                new ReplaceOptions { IsUpsert = true }));
    }

    /// <inheritdoc />
    public async Task FlushAllAsync(Guid userId, UnreadEntryType entryType, Guid parentId)
    {
        // Same reason as FlushAsync: a deleted entity must not come back through
        // "mark everything as read".
        var entityIds = await Collection.Distinct(c => c.EntityId,
                Filter.Eq(c => c.ParentId, parentId) &
                Filter.Eq(c => c.EntryType, entryType) &
                Filter.Eq(c => c.IsRemoved, false))
            .ToListAsync();
        var rightNow = _dateTimeProvider.Now.UtcDateTime;
        var markers = entityIds
            .Select(id => Upsert(new UnreadCounter
            {
                UserId = userId,
                EntityId = id,
                ParentId = parentId,
                EntryType = entryType,
                LastReadUtc = rightNow,
                Counter = 0
            }))
            .ToArray();

        // Nothing unread under this parent is the ordinary state of a user who
        // reads everything, and a bulk write refuses an empty batch.
        if (markers.Length == 0)
        {
            return;
        }

        await UpsertAsync(() => Collection.BulkWriteAsync(markers));
    }

    /// <inheritdoc />
    public async Task ChangeParentAsync(Guid parentId, UnreadEntryType entryType, Guid newParentId)
    {
        await Collection.UpdateManyAsync(
            Filter.Eq(c => c.ParentId, parentId) &
            Filter.Eq(c => c.EntryType, entryType),
            Update.Set(c => c.ParentId, newParentId));
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastReadTimeAsync(Guid userId, Guid entityId, UnreadEntryType entryType)
    {
        // Sorted, and not "whichever document comes back first": the unique index
        // on (UserId, EntityId, EntryType) makes one marker the only lawful state,
        // but a database that predates the index keeps what it already had — the
        // startup assertion logs the conflict and moves on. The latest read is the
        // answer - the same one the aggregate reads reach through Min(Counter).
        var counter = await Collection
            .Find(
                Filter.Eq(c => c.UserId, userId) &
                Filter.Eq(c => c.EntityId, entityId) &
                Filter.Eq(c => c.EntryType, entryType) &
                Filter.Eq(c => c.IsRemoved, false))
            .SortByDescending(c => c.LastReadUtc)
            .FirstOrDefaultAsync();

        return counter?.LastReadUtc;
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, DateTime>> GetLastReadTimesAsync(Guid userId, UnreadEntryType entryType, params Guid[] entityIds)
    {
        var counters = await Collection
            .Find(
                Filter.Eq(c => c.UserId, userId) &
                Filter.In(c => c.EntityId, entityIds) &
                Filter.Eq(c => c.EntryType, entryType) &
                Filter.Eq(c => c.IsRemoved, false))
            .ToListAsync();

        // Grouped for the same reason: a plain ToDictionary throws on a duplicated
        // entity, and the caller is the jump to the first unread post, which would
        // then fail whole on a database whose unique index was never created.
        return counters
            .GroupBy(c => c.EntityId)
            .ToDictionary(g => g.Key, g => g.Max(c => c.LastReadUtc));
    }

    /// <summary>
    /// The key the collection's unique index enforces. Every write that can meet
    /// an existing marker addresses it by exactly this triple, so the filter is
    /// spelled out once and cannot drift between the write paths.
    /// </summary>
    private static FilterDefinition<UnreadCounter> Key(Guid userId, Guid entityId, UnreadEntryType entryType) =>
        Filter.Eq(c => c.UserId, userId) &
        Filter.Eq(c => c.EntityId, entityId) &
        Filter.Eq(c => c.EntryType, entryType);

    private static ReplaceOneModel<UnreadCounter> Upsert(UnreadCounter marker) =>
        new(Key(marker.UserId, marker.EntityId, marker.EntryType), marker) { IsUpsert = true };

    /// <summary>
    /// An upsert is not atomic against another upsert on the same key: both can
    /// find no document, both then insert, and the unique index refuses the second
    /// one. Two tabs, a double click on "mark as read" or a retried request over a
    /// mobile network is exactly that race. One retry settles it — the winner's
    /// document is in place by then, so the retry matches it and replaces instead
    /// of inserting.
    /// </summary>
    private static async Task UpsertAsync(Func<Task> write)
    {
        try
        {
            await write();
        }
        catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await write();
        }
        catch (MongoBulkWriteException e) when (
            e.WriteErrors.Any(error => error.Category == ServerErrorCategory.DuplicateKey))
        {
            await write();
        }
    }
}
