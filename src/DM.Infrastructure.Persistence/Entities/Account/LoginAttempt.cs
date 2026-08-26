using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for tracking login attempts (rate limiting and lockout)
/// </summary>
[Table("LoginAttempts")]
public class LoginAttempt
{
    /// <summary>
    /// Composite key: lowercase email and client address, formed by the domain
    /// (LoginAttemptOrigin.Key)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Lowercase email, denormalized out of <see cref="Key"/> so a successful
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
    /// Last failed attempt timestamp (UTC). What the retention sweep reads.
    /// </summary>
    public DateTime LastAttemptUtc { get; set; }

    /// <summary>
    /// Lockout start timestamp (null if not locked)
    /// </summary>
    public DateTime? LockoutStartUtc { get; set; }
}
