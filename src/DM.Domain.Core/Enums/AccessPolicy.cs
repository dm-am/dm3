using System;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Individual user composite access policy. Two ban scopes exist and no others:
/// what each one forbids is in <see cref="Authorization.AccessRestrictions" />.
/// </summary>
[Flags]
public enum AccessPolicy
{
    /// <summary>
    /// No restrictions
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
