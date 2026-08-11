using System;
using System.Collections.Generic;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for user authentication
/// </summary>
[MongoCollectionName("UserSessions")]
public class UserSession
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Authentication sessions
    /// </summary>
    public List<Session> Sessions { get; set; } = [];
}

/// <summary>
/// DAL model for authentication session
/// </summary>
public class Session
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Expiration moment (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpirationUtc { get; set; }

    /// <summary>
    /// Persistence flag
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    /// Session creation time (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Client IP address at session creation
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent header value
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Parsed device/browser info (derived from UserAgent)
    /// </summary>
    public string? DeviceInfo { get; set; }
}
