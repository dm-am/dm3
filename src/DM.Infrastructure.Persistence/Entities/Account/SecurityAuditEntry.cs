using System;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for security audit log entry (stored in MongoDB)
/// </summary>
[MongoCollectionName("SecurityAuditLog")]
public class SecurityAuditEntry
{
    /// <summary>
    /// Entry identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of security event (stored as int for MongoDB compatibility)
    /// </summary>
    public int EventType { get; set; }

    /// <summary>
    /// When the event occurred (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// IP address of the client
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Parsed device info (e.g., "Chrome на Windows")
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Additional details (e.g., error reason, new email for email change)
    /// </summary>
    public string? Details { get; set; }
}
