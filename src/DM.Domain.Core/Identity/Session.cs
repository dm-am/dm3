using System;

namespace DM.Domain.Core.Identity;

/// <summary>
/// DTO model for user authentication session
/// </summary>
public class Session
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Session persistence flag
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    /// Expiration date (UTC)
    /// </summary>
    public DateTimeOffset ExpirationUtc { get; set; }

    /// <summary>
    /// Session creation time (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Client IP address at session creation
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Parsed device/browser description
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Flag indicating if this is the current session
    /// </summary>
    public bool IsCurrent { get; set; }
}
