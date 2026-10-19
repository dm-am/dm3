namespace DM.Web.API.Shared.RateLimiting;

/// <summary>
/// Names of the rate-limit policies the API registers.
/// </summary>
/// <remarks>
/// A name that matches no registered policy throws on the first request to that
/// endpoint, not at startup; constants make the pairing a compile-time one.
/// </remarks>
internal static class RateLimitPolicies
{
    /// <summary>Authentication endpoints: login, registration, password reset.</summary>
    public const string Auth = "auth";

    /// <summary>
    /// Settings of the second factor: switching it on, off, and reissuing the
    /// recovery codes.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Auth" />: switching the factor on spends four of its five
    /// requests in one sitting, and behind a shared address a neighbour's
    /// sign-in spends the rest.
    /// </remarks>
    public const string TwoFactor = "two-factor";

    /// <summary>Username availability check on the registration form.</summary>
    public const string UsernameCheck = "username-check";

    /// <summary>Email availability check on the registration form.</summary>
    public const string EmailCheck = "email-check";

    /// <summary>File uploads.</summary>
    public const string Uploads = "uploads";

    /// <summary>Expensive reads: chat availability, full-text search.</summary>
    public const string Sliding = "sliding";

    /// <summary>Ordinary authenticated writes.</summary>
    public const string Default = "default";
}
