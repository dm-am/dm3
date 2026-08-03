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
    /// Aggregation entity identifier
    /// </summary>
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