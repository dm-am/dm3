namespace DM.Domain.Core.Configuration;

/// <summary>
/// Invitations: the single definition of how long one stays valid.
/// </summary>
/// <remarks>
/// A constant and not a setting: the value is baked into invitations already sent.
/// </remarks>
public static class InvitationPolicy
{
    /// <summary>
    /// Days an invitation remains valid after it was created.
    /// </summary>
    public const int ExpirationDays = 30;

    /// <summary>
    /// The moment an invitation created at <paramref name="createdUtc" /> stops
    /// being accepted.
    /// </summary>
    public static DateTimeOffset ExpiresAt(DateTimeOffset createdUtc) =>
        createdUtc.AddDays(ExpirationDays);
}
