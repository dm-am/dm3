namespace DM.Domain.Account.Configuration;

/// <summary>
/// Configuration for authentication behavior
/// </summary>
public class AuthenticationConfiguration
{
    /// <summary>
    /// Session lifetime in hours for non-persistent sessions ("Remember Me" unchecked).
    /// </summary>
    /// <remarks>
    /// Counted from the moment the session is issued, not from the last request:
    /// the expiry is stamped once at creation and only moves when the session is
    /// used inside the refresh window below. A session left untouched longer than
    /// that window dies on the calendar rather than on inactivity.
    ///
    /// The value that ships is in appsettings; the initializer here is the
    /// fallback for a host that binds no section at all, so a number quoted in
    /// this file describes nothing that runs.
    /// </remarks>
    public int SessionExpirationHours { get; set; } = 24;

    /// <summary>
    /// Session lifetime in days for persistent sessions ("Remember Me" checked).
    /// </summary>
    /// <remarks>
    /// Counted from issue, see <see cref="SessionExpirationHours"/>.
    /// </remarks>
    public int PersistentSessionExpirationDays { get; set; } = 365;

    /// <summary>
    /// Session refresh window in minutes.
    /// When a session is about to expire within this window, it gets automatically extended.
    /// </summary>
    public int SessionRefreshMinutes { get; set; } = 10080;

    /// <summary>
    /// Activity tracking interval in minutes.
    /// User's LastActivityUtc is updated at most once per this interval to reduce database writes.
    /// Default: 1 minute
    /// </summary>
    public int ActivityTrackingMinutes { get; set; } = 1;

    /// <summary>
    /// Login attempt cache expiration in hours.
    /// Failed login attempts are tracked for this duration for rate limiting.
    /// Default: 24 hours
    /// </summary>
    public int LoginAttemptExpirationHours { get; set; } = 24;

    /// <summary>
    /// Progressive delay configuration for failed login attempts.
    /// Each entry: [attemptThreshold, delaySeconds]
    /// Default: 0-2 attempts = 0s, 3-4 = 1s, 5-9 = 5s, 10+ = 30s
    /// </summary>
    public int[][] LoginDelaySchedule { get; set; } =
    {
        new[] { 3, 1 },    // 3+ attempts: 1 second delay
        new[] { 5, 5 },    // 5+ attempts: 5 seconds delay
        new[] { 10, 30 }   // 10+ attempts: 30 seconds delay
    };

    /// <summary>
    /// Number of failed login attempts before account is temporarily locked.
    /// Default: 15 attempts
    /// </summary>
    public int AccountLockoutThreshold { get; set; } = 15;

    /// <summary>
    /// Account lockout duration in minutes.
    /// Default: 30 minutes
    /// </summary>
    public int AccountLockoutDurationMinutes { get; set; } = 30;
}
