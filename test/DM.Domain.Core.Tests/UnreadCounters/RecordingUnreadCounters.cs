using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;

namespace DM.Domain.Core.Tests.UnreadCounters;

/// <summary>
/// A counters store that writes nothing and remembers everything it was asked to.
/// </summary>
/// <remarks>
/// Written by hand rather than mocked, because the project it lives in references
/// the kernel and nothing else on purpose: a test assembly that dragged an
/// implementation in beside the kernel would let a dependency reach the kernel's
/// types without the build saying so, which is the property the kernel exists for.
///
/// Only the four members of the marker lifecycle do anything. The rest of the
/// contract is reads and arithmetic over markers that were never written, and a
/// reservation has no business calling any of it — so they refuse rather than
/// return an empty answer that would let a wrong call look right.
/// </remarks>
internal sealed class RecordingUnreadCounters : IUnreadCountersRepository
{
    private readonly List<(Guid EntityId, Guid? ParentId, UnreadEntryType EntryType, Guid[]? Readers)> written = [];
    private readonly List<(Guid EntityId, UnreadEntryType EntryType, Guid[]? Readers)> takenBack = [];

    /// <summary>Type of the exception the next write refuses with, if any.</summary>
    public Func<Exception>? RefuseWriteOf { get; set; }

    /// <summary>Entry type whose write is refused, when <see cref="RefuseWriteOf" /> is set.</summary>
    public UnreadEntryType? RefusedEntryType { get; set; }

    /// <summary>Exception every take-back throws with, if any.</summary>
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

    public Task CreateMarkerAsync(Guid entityId, UnreadEntryType entryType) =>
        CreateMarkerAsync(entityId, entityId, entryType);

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

    public Task ChangeParentAsync(Guid parentId, UnreadEntryType entryType, Guid newParentId) =>
        Unexpected<object>();

    public Task<DateTime?> GetLastReadTimeAsync(Guid userId, Guid entityId, UnreadEntryType entryType) =>
        Unexpected<DateTime?>();

    public Task<IDictionary<Guid, DateTime>> GetLastReadTimesAsync(
        Guid userId, UnreadEntryType entryType, params Guid[] entityIds) =>
        Unexpected<IDictionary<Guid, DateTime>>();
}
