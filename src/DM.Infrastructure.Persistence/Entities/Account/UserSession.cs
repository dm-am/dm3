using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for an authentication session: one row per session.
/// </summary>
/// <remarks>
/// A lookup always names both halves of the token: the row carries its owner,
/// and FindUserSession filters by (SessionId, UserId), so a session id that
/// exists but belongs to somebody else resolves to nothing.
/// </remarks>
[Table("UserSessions")]
public class UserSession
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Owning user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Session creation time (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Expiration moment (UTC)
    /// </summary>
    public DateTimeOffset ExpirationUtc { get; set; }

    /// <summary>
    /// Persistence flag
    /// </summary>
    public bool Persistent { get; set; }

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
