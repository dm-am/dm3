using DM.Domain.Core.Enums;
using System;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for unread entries count
/// </summary>
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
    /// Last read moment
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastRead { get; set; }

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
}