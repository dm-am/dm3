using System;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for tracking login attempts (rate limiting and lockout)
/// </summary>
[MongoCollectionName("LoginAttempts")]
public class LoginAttempt
{
    /// <summary>
    /// Composite identifier: lowercase email and client address
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Lowercase email, denormalized out of <see cref="Id"/> so a successful
    /// login can clear the account's records from every address at once
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Client address the attempts came from, null when it could not be determined
    /// </summary>
    public string? IpAddress { get; set; }

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
