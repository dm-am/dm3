using System;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Individual user composite access policy. Two ban scopes exist and no others:
/// what each one forbids is in <see cref="Authorization.AccessRestrictions" />.
/// </summary>
/// <remarks>
/// The type answers two questions, and [Flags] is right for one of them. On a ban
/// row it is the scope that ban was issued with, and exactly one of the two members
/// is legal there - CreateBanValidator refuses anything else with 400, because the
/// fallback to the strictest scope handed a moderator a full ban without telling him
/// he had asked for something else. On a user it is the union of every ban in force,
/// which is what EffectiveAccessPolicyAt builds and MaySpeak reads.
///
/// Bit 1 (value 2) is deliberately undefined and stays that way: it is the gap left
/// by an older third scope, and filling it would make a value no validator, screen
/// or document knows look legal.
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
