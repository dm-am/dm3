using System;

namespace DM.Domain.Core.Configuration;

/// <summary>
/// Invitations: the single definition of how long one stays valid.
/// </summary>
/// <remarks>
/// The number was written seven times across four files, and the two copies that
/// mattered were separate: services enforced their own constant, repositories
/// projected a literal into the expiry date shown to the invitee. Nothing made
/// them agree, so a change to one would have produced an invitation that displays
/// one deadline and is refused at another.
///
/// A constant and not a setting: it is a product rule with no operator dial, and
/// the value is baked into invitations already sent.
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
