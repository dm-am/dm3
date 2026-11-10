namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Who is attempting to log in: the account, and where the attempt came from.
/// </summary>
/// <remarks>
/// Counted per pair so that wrong passwords from one address cannot lock the
/// account out everywhere; a spread-out attack meets the per-address rate limit.
/// </remarks>
/// <param name="IpAddress">Client address, null when it cannot be determined.</param>
public readonly record struct LoginAttemptOrigin(string Email, string? IpAddress)
{
    /// <summary>
    /// Normalized email, the key of "everything this account has attempted".
    /// </summary>
    public string NormalizedEmail => Email.ToLowerInvariant();

    /// <summary>
    /// Storage key of the counter. The single place the pair is composed.
    /// </summary>
    public string Key => $"{NormalizedEmail}|{IpAddress ?? "unknown"}";
}
