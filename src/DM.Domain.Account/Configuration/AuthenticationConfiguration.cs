namespace DM.Domain.Account.Configuration;

/// <summary>
/// Configuration for authentication behavior
/// </summary>
public class AuthenticationConfiguration
{
    /// <summary>
    /// Session expiration in hours for non-persistent sessions ("Remember Me" unchecked).
    /// User will be logged out after this period of inactivity.
    /// Default: 24 hours
    /// </summary>
    public int SessionExpirationHours { get; set; } = 24;

    /// <summary>
    /// Session expiration for persistent sessions ("Remember Me" checked) in days.
    /// User will be logged out after this period of inactivity.
    /// Default: 30 days
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

    /// <summary>
    /// Domain the session cookie is scoped to. Empty means host-only.
    /// </summary>
    /// <remarks>
    /// A host-only cookie belongs to the exact name that issued it, so a visitor
    /// who follows a link to another address of the same site arrives
    /// unauthenticated and signs in again. Naming the registrable domain here
    /// hands the cookie to every host under it, and one session then covers all
    /// the addresses the site answers on.
    ///
    /// Empty by default, and deliberately: the value is only safe once every
    /// host under that domain is this application. A domain shared with anything
    /// else hands that thing the session cookie of every visitor.
    /// </remarks>
    public string SessionCookieDomain { get; set; } = "";
}
