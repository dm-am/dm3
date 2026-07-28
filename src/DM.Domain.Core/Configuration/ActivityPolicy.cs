using System;

namespace DM.Domain.Core.Configuration;

/// <summary>
/// Product rules for what counts as a recently active user.
/// </summary>
/// <remarks>
/// These are product decisions, not technical ones, and several screens have to
/// agree on them: listing filters, subscriber ordering and the popularity score
/// all derive "is this user active" from the same threshold. Held here so that
/// changing the definition is one edit rather than four across two assemblies —
/// a partial change produces screens that quietly disagree with each other.
/// </remarks>
public static class ActivityPolicy
{
    /// <summary>
    /// How long after their last visit a user still counts as active.
    /// </summary>
    public static readonly TimeSpan ActivePeriod = TimeSpan.FromDays(30);

    /// <summary>
    /// How long after their last visit a user is still shown as online.
    /// </summary>
    public static readonly TimeSpan OnlinePeriod = TimeSpan.FromMinutes(5);
}
