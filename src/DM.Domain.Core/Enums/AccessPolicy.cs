namespace DM.Domain.Core.Enums;

/// <summary>
/// Individual user composite access policy. Two ban scopes exist and no others:
/// what each one forbids is in <see cref="Authorization.AccessRestrictions" />.
/// </summary>
/// <remarks>
/// On a ban row exactly one member is legal (CreateBanValidator refuses anything
/// else); on a user it is the union of every ban in force (EffectiveAccessPolicyAt).
/// Bit 1 (value 2) stays undefined: no validator, screen or document knows it.
/// </remarks>
[Flags]
public enum AccessPolicy
{
    /// <summary>
    /// No restrictions. A user's resting state, never a ban's scope.
    /// </summary>
    NotSpecified = 0,

    /// <summary>
    /// Ordinary ban: public speech is silenced, own games and blogs stay open
    /// </summary>
    DemocraticBan = 1 << 0,

    /// <summary>
    /// Full ban: nothing may be sent, and authentication itself fails
    /// </summary>
    FullBan = 1 << 2
}
