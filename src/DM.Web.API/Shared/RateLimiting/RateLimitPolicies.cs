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
