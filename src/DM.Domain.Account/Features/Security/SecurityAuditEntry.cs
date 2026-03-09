using System;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Security audit log entry (for API responses)
/// </summary>
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
    /// Type of security event
    /// </summary>
    public SecurityEventType EventType { get; set; }

    /// <summary>
    /// When the event occurred (UTC)
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// IP address of the client
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Parsed device info (e.g., "Chrome на Windows")
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Additional details
    /// </summary>
    public string? Details { get; set; }
}
