using DM.Domain.Core.Enums;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for unread entries count
/// </summary>
/// <remarks>
/// IRemovable, not ISoftDeletable: the counter is derived data with no author and
/// no audit story, and nothing ever wrote the two audit fields the wider contract
/// promises. Declaring a contract the code does not honor is worse than not
/// declaring it — a reader trusts the fields and finds them empty.
///
/// Deliberately excluded from the global soft-delete query filter: the write
/// paths have to see tombstone rows — an upsert over a tombstone revives the
/// marker for a participant added back on purpose, while a flush has to ignore
/// it — so each read spells its own IsRemoved predicate.
/// </remarks>
[Table("UnreadCounters")]
public class UnreadCounter : IRemovable
{
    /// <summary>
    /// User identifier, part of the primary key.
    /// <see cref="Guid.Empty"/> for anonymous counter
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Entity identifier, part of the primary key
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Entry type, part of the primary key
    /// </summary>
    public UnreadEntryType EntryType { get; set; }

    /// <summary>
    /// What a parent-scoped read of this marker aggregates under — a container
    /// for some entities and the reader themselves for others
    /// </summary>
    /// <remarks>
    /// "Aggregation entity identifier" named only the first half, and the second
    /// is what the delicate code depends on. Which of the two a marker carries is
    /// decided by the overload that created it, and the rule is written once, at
    /// IUnreadCountersRepository, rather than restated here — see CODE_STYLE.md
    /// on where the reason for a contract belongs.
    /// No foreign key: the reference is polymorphic by EntryType, like every
    /// polymorphic reference of the project, so integrity stays on the application.
    /// </remarks>
    public Guid ParentId { get; set; }

    /// <summary>
    /// Last read moment (UTC)
    /// </summary>
    public DateTime LastReadUtc { get; set; }

    /// <summary>
    /// Counter itself
    /// </summary>
    public int Counter { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Moment the marker was tombstoned (UTC), null while it is live.
    /// </summary>
    /// <remarks>
    /// What the retention sweep reads. A tombstone is only needed for as long as
    /// something can still ask to mark the deleted entity as read, which is
    /// minutes; without a moment to expire from it stayed forever, one row per
    /// user per deleted entity. The sweep deletes by this column, and a live
    /// marker's NULL never matches the cutoff predicate.
    /// </remarks>
    public DateTime? RemovedUtc { get; set; }
}
