using System;
using DM.Domain.Account.Features.Security;

namespace DM.Web.API.Features.Account.Security;

/// <summary>
/// Security event for API responses
/// </summary>
public class SecurityEvent
{
    /// <summary>
    /// Event identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of security event
    /// </summary>
    public SecurityEventType EventType { get; set; }

    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>
    /// IP address of the client
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Device/browser info
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Additional details
    /// </summary>
    public string? Details { get; set; }
}
