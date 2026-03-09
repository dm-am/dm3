using System;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// User session information
/// </summary>
public class Session
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Whether this is the current session
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Session persistence flag (remember me)
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    /// Session expiration date
    /// </summary>
    public DateTimeOffset ExpirationDate { get; set; }

    /// <summary>
    /// Session creation date (UTC)
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Device/browser description (e.g., "Chrome on Windows")
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Client IP address at login
    /// </summary>
    public string? IpAddress { get; set; }
}
