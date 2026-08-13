using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.UnreadCounters;

/// <summary>
/// One unread marker, named the way the store will write it.
/// </summary>
/// <remarks>
/// <para>
/// What ParentId holds is the one thing about these markers that cannot be read
/// off a signature. It is whatever answers "all of mine" for the entity in
/// question, and that is two different kinds of thing.
/// </para>
/// <para>
/// <see cref="UnderParent" /> gets a container: a topic is parented by its board,
/// a room by its game, a publication by its blog. Those markers are anonymous —
/// one per entity, shared by every reader who has not opened it yet — and a
/// parent-scoped read means "how much is unread in this board".
/// </para>
/// <para>
/// <see cref="ForReaders" /> takes a list of readers instead and parents each
/// marker by the reader themselves. A conversation has no container, so without
/// this "all my conversations" would not be a parent-scoped read at all.
/// <see cref="SelfParented" /> is the degenerate case: the entity is its own
/// parent.
/// </para>
/// <para>
/// Both meanings live in one field on purpose — a parent-scoped aggregate is one
/// query either way — but nothing in the storage tells them apart, so a write
/// that stamps the wrong kind does not fail, it silently drops the entity out of
/// every total it belonged to.
/// </para>
/// </remarks>
public readonly record struct UnreadMarker
{
    private UnreadMarker(Guid entityId, Guid parentId, UnreadEntryType entryType, IReadOnlyCollection<Guid>? readers)
    {
        EntityId = entityId;
        ParentId = parentId;
        EntryType = entryType;
        Readers = readers;
    }

    /// <summary>Entity the marker counts unread entries of.</summary>
    public Guid EntityId { get; }

    /// <summary>Whatever answers "all of mine" for this entity.</summary>
    public Guid ParentId { get; }

    /// <summary>Kind of entry being counted.</summary>
    public UnreadEntryType EntryType { get; }

    /// <summary>Readers this marker belongs to, or null when it belongs to everybody.</summary>
    public IReadOnlyCollection<Guid>? Readers { get; }

    /// <summary>An anonymous marker inside a container.</summary>
    public static UnreadMarker UnderParent(Guid entityId, Guid parentId, UnreadEntryType entryType) =>
        new(entityId, parentId, entryType, null);

    /// <summary>An anonymous marker of an entity that is its own container.</summary>
    public static UnreadMarker SelfParented(Guid entityId, UnreadEntryType entryType) =>
        UnderParent(entityId, entityId, entryType);

    /// <summary>
    /// A marker per reader, for an entity that has no container of its own.
    /// </summary>
    /// <remarks>
    /// The list is materialised here rather than kept as a query: the same readers
    /// have to be written and, if the write is taken back, taken back — and a
    /// sequence enumerated twice can answer differently the second time.
    /// </remarks>
    public static UnreadMarker ForReaders(Guid entityId, UnreadEntryType entryType, IEnumerable<Guid> readers) =>
        new(entityId, entityId, entryType, readers.Distinct().ToArray());
}
