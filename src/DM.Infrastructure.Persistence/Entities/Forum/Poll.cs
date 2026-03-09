using System;
using System.Collections.Generic;
using DM.Infrastructure.Persistence.Entities.DataContracts;
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
    /// Moment from
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Moment to
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Poll is global
    /// </summary>
    public bool Global { get; set; }

    /// <summary>
    /// Question text
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Options
    /// </summary>
    public List<PollOption> Options { get; set; } = [];

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
