using System;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Individual user composite access policy
/// </summary>
[Flags]
public enum AccessPolicy
{
    /// <summary>
    /// No restrictions
    /// </summary>
    NotSpecified = 0,

    /// <summary>
    /// Democratic ban restrictions
    /// </summary>
    DemocraticBan = 1 << 0,

    /// <summary>
    /// Full ban restrictions
    /// </summary>
    FullBan = 1 << 2,

    /// <summary>
    /// Global chat ban restrictions
    /// </summary>
    GlobalChatBan = 1 << 3,

    /// <summary>
    /// Content editing restrictions
    /// </summary>
    RestrictContentEditing = 1 << 4
}
