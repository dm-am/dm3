namespace DM.Web.API.Shared.RateLimiting;

/// <summary>
/// Names of the rate-limit policies the API registers.
/// </summary>
/// <remarks>
/// Both sides of the handshake are string-matched by the framework: a policy is
/// registered under a name and a controller asks for it by name through
/// <c>[EnableRateLimiting]</c>. A name that matches nothing is not a startup
/// error — it throws on the first request to that endpoint. Constants make the
/// pairing a compile-time one.
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
    /// Not <see cref="Auth" />, although the surface looks like one. That budget
    /// is five requests an address may spend in a minute, and switching the
    /// factor on costs four of them in one sitting - after the sign-in that got
    /// the person to the screen has already spent one. A single mistyped
    /// confirmation code then answered 429 instead of "that code did not match",
    /// and behind carrier-grade NAT, where the address is a neighbourhood, it did
    /// so without anybody mistyping anything.
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

    /// <summary>Ordinary authenticated writes: preferences, subscriptions, invitations, notepads, game and blog management.</summary>
    public const string Default = "default";
}
