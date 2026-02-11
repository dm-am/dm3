namespace DM.Services.Authentication.Configuration;

/// <summary>
/// Configuration for authentication behavior
/// </summary>
public class AuthenticationConfiguration
{
    /// <summary>
    /// Session expiration in hours.
    /// Default: 8760 hours (1 year) - users are never logged out
    /// </summary>
    public int SessionExpirationHours { get; set; } = 8760;

    /// <summary>
    /// Session expiration for persistent sessions in days.
    /// Note: All sessions are created as persistent (no "Remember me" checkbox).
    /// Default: 365 days (1 year)
    /// </summary>
    public int PersistentSessionExpirationDays { get; set; } = 365;

    /// <summary>
    /// Session refresh window in minutes.
    /// When a session is about to expire within this window, it gets automatically extended.
    /// Default: 10080 minutes (7 days)
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
