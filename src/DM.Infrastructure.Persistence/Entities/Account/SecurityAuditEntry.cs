using System;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Account.Features.Security;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for security audit log entry: append-only stream, swept by the
/// retention pass (the log holds addresses and user agents — personal data).
/// </summary>
[Table("SecurityAuditEntries")]
public class SecurityAuditEntry
{
    /// <summary>
    /// Entry identifier
    /// </summary>
    public Guid SecurityAuditEntryId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of security event
    /// </summary>
    public SecurityEventType EventType { get; set; }

    /// <summary>
    /// When the event occurred (UTC)
    /// </summary>
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
