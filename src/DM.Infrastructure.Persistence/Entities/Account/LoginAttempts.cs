using System;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for tracking login attempts (rate limiting and lockout)
/// </summary>
[MongoCollectionName("LoginAttempts")]
public class LoginAttempts
{
    /// <summary>
    /// Email identifier (lowercase email)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Number of failed login attempts
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Last failed attempt timestamp
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastAttemptUtc { get; set; }

    /// <summary>
    /// Lockout start timestamp (null if not locked)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LockoutStartUtc { get; set; }
}
