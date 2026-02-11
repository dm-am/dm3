using System;
using DM.Services.DataAccess.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Services.DataAccess.BusinessObjects.Users;

/// <summary>
/// DAL model for tracking login attempts (rate limiting and lockout)
/// </summary>
[MongoCollectionName("LoginAttempts")]
public class LoginAttempts
{
    /// <summary>
    /// Login identifier (lowercase login/email)
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
