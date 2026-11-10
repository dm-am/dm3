using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;

namespace DM.Domain.Core.Tests.UnreadCounters;

// Members outside the marker lifecycle refuse, so a wrong call cannot look right.
internal sealed class RecordingUnreadCounters : IUnreadCountersRepository
{
    private readonly List<(Guid EntityId, Guid? ParentId, UnreadEntryType EntryType, Guid[]? Readers)> written = [];
    private readonly List<(Guid EntityId, UnreadEntryType EntryType, Guid[]? Readers)> takenBack = [];

    public Func<Exception>? RefuseWriteOf { get; set; }

    public UnreadEntryType? RefusedEntryType { get; set; }

    public Func<Exception>? RefuseTakeBack { get; set; }

    public IReadOnlyList<(Guid EntityId, Guid? ParentId, UnreadEntryType EntryType, Guid[]? Readers)> Written => written;

    public IReadOnlyList<(Guid EntityId, UnreadEntryType EntryType, Guid[]? Readers)> TakenBack => takenBack;

    public Task CreateMarkerAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds)
    {
        Refuse(entryType);
        written.Add((entityId, null, entryType, userIds.ToArray()));
        return Task.CompletedTask;
    }

    public Task CreateMarkerAsync(Guid entityId, Guid parentId, UnreadEntryType entryType)
    {
        Refuse(entryType);
        written.Add((entityId, parentId, entryType, null));
        return Task.CompletedTask;
    }


    public Task DeleteAsync(Guid entityId, UnreadEntryType entryType)
    {
        if (RefuseTakeBack != null) throw RefuseTakeBack();
        takenBack.Add((entityId, entryType, null));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> userIds)
    {
        if (RefuseTakeBack != null) throw RefuseTakeBack();
        takenBack.Add((entityId, entryType, userIds.ToArray()));
        return Task.CompletedTask;
    }

    private void Refuse(UnreadEntryType entryType)
    {
        if (RefuseWriteOf != null && RefusedEntryType == entryType)
        {
            throw RefuseWriteOf();
        }
    }

    private static Task<T> Unexpected<T>() =>
        throw new NotSupportedException("a reservation reads nothing and counts nothing");

    public Task IncrementAsync(Guid entityId, UnreadEntryType entryType) => Unexpected<object>();

    public Task IncrementExcludingAsync(Guid entityId, UnreadEntryType entryType, Guid excludeUserId) =>
        Unexpected<object>();

    public Task DecrementAsync(Guid entityId, UnreadEntryType entryType, DateTimeOffset createDate) =>
        Unexpected<object>();

    public Task<IDictionary<Guid, int>> SelectByParentsAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] parentIds) => Unexpected<IDictionary<Guid, int>>();

    public Task<IDictionary<Guid, int>> SelectTotalUnreadByParentsAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] parentIds) => Unexpected<IDictionary<Guid, int>>();

    public Task<IDictionary<Guid, int>> SelectByEntitiesAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] entityIds) => Unexpected<IDictionary<Guid, int>>();

    public Task FlushAsync(Guid userId, UnreadEntryType entryType, Guid entityId) => Unexpected<object>();

    public Task FlushAllAsync(Guid userId, UnreadEntryType entryType, Guid parentId) => Unexpected<object>();

    public Task ChangeParentAsync(Guid entityId, UnreadEntryType entryType, Guid newParentId) =>
        Unexpected<object>();

    public Task<DateTime?> GetLastReadTimeAsync(Guid userId, Guid entityId, UnreadEntryType entryType) =>
        Unexpected<DateTime?>();

    public Task<IDictionary<Guid, DateTime>> GetLastReadTimesAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] entityIds) =>
        Unexpected<IDictionary<Guid, DateTime>>();
}
