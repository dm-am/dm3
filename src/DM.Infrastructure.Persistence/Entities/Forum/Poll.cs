using System;
using System.Collections.Generic;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Forum;

/// <summary>
/// DAL model for poll
/// </summary>
[MongoCollectionName("Polls")]
public class Poll : IRemovable
{
    /// <summary>
    /// Identifier
    /// </summary>
    [BsonId]
    public Guid Id { get; set; }

    /// <summary>
    /// Start moment (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime StartsUtc { get; set; }

    /// <summary>
    /// End moment (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime EndsUtc { get; set; }

    /// <summary>
    /// Question text
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Optional description/details for the poll
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Options
    /// </summary>
    public List<PollOption> Options { get; set; } = [];

    /// <summary>
    /// Whether poll is anonymous (votes are hidden)
    /// </summary>
    public bool IsAnonymous { get; set; } = true;

    /// <summary>
    /// Removed flag
    /// </summary>
    public bool IsRemoved { get; set; }
}

/// <summary>
/// DAL model for poll option
/// </summary>
public class PollOption
{
    /// <summary>
    /// Identifier
    /// </summary>
    [BsonId]
    public Guid Id { get; set; }

    /// <summary>
    /// Answer text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Voted users identifiers
    /// </summary>
    public List<Guid> UserIds { get; set; } = [];
}
