using DM.Domain.Core.Enums;
using System;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for unread entries count
/// </summary>
/// <remarks>
/// IRemovable, not ISoftDeletable: the counter is derived data with no author and
/// no audit story, and nothing ever wrote the two audit fields the wider contract
/// promises. Declaring a contract the code does not honor is worse than not
/// declaring it — a reader trusts the fields and finds them empty.
/// </remarks>
[MongoCollectionName("UnreadCounters")]
[BsonIgnoreExtraElements]
public class UnreadCounter : IRemovable
{
    /// <summary>
    /// User identifier
    /// <see cref="Guid.Empty"/> for anonymous counter
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Entity identifier
    /// </summary>
    public Guid EntityId { get; set; }

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
    /// </remarks>
    public Guid ParentId { get; set; }

    /// <summary>
    /// Last read moment (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastReadUtc { get; set; }

    /// <summary>
    /// Entry type
    /// </summary>
    public UnreadEntryType EntryType { get; set; }

    /// <summary>
    /// Counter itself
    /// </summary>
    public int Counter { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Moment the marker was tombstoned (UTC), absent while it is live.
    /// </summary>
    /// <remarks>
    /// What the collection's TTL index reads. A tombstone is only needed for as
    /// long as something can still ask to mark the deleted entity as read, which
    /// is minutes; without a moment to expire from it stayed forever, one document
    /// per user per deleted entity. A live marker leaves the element absent, and a
    /// TTL index ignores documents whose field is not a date, so nothing collects
    /// what is still in use.
    /// </remarks>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? RemovedUtc { get; set; }
}
