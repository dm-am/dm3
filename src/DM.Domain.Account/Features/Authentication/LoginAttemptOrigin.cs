namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Who is attempting to log in: the account, and where the attempt came from.
/// </summary>
/// <remarks>
/// Failed attempts are counted per pair, not per account. Counted per account
/// alone, fifteen deliberately wrong passwords lock any user out for half an
/// hour — including every moderator and admin — from any address, repeatable
/// indefinitely. Keying on the pair keeps brute force bounded (one address still
/// locks itself out after the threshold) and leaves the account reachable from
/// everywhere else; a spread-out attack is held down by the per-address rate
/// limit on the auth endpoints instead.
/// </remarks>
/// <param name="Email">Account being attempted.</param>
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
