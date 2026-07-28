using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Identity;

/// <summary>
/// A restriction a ban imposes, with the window it is in force for.
/// </summary>
/// <remarks>
/// The window is part of the record on purpose. A ban row outlives the ban: it
/// stays for the moderation history, so "the user has a ban row" and "the user is
/// banned right now" are different questions, and only the second one may
/// restrict anything.
/// </remarks>
/// <param name="Policy">What the ban forbids</param>
/// <param name="StartedUtc">In force from</param>
/// <param name="EndedUtc">In force until</param>
public readonly record struct AccessRestriction(
    AccessPolicy Policy,
    DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc)
{
    /// <summary>
    /// Whether the restriction is in force at the given moment.
    /// </summary>
    /// <remarks>
    /// This is the single definition of "banned right now". Any query that lists
    /// active bans must express the same predicate, or the moderation screens and
    /// the enforcement disagree about the same user.
    /// </remarks>
    public bool IsInForceAt(DateTimeOffset moment) =>
        StartedUtc <= moment && moment < EndedUtc;
}
