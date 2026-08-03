using System;

namespace DM.Domain.Core.Configuration;

/// <summary>
/// Product rules for what counts as a recently active user.
/// </summary>
/// <remarks>
/// These are product decisions, not technical ones, and several screens have to
/// agree on them: listing filters, subscriber ordering and the popularity score
/// all derive "is this user active" from the same threshold. Held here so that
/// changing the definition is one edit for the whole server rather than one per
/// query, because a partial change produces screens that quietly disagree with
/// each other.
///
/// One edit for the server, not for the product. The browser paints the green
/// "online" dot on its own clock and cannot read this file, so
/// <see cref="OnlinePeriod"/> has a second copy in the client constant
/// <c>ONLINE_THRESHOLD_MINUTES</c>. A client test reads this file and compares
/// the two numbers, so changing one of them alone fails there instead of
/// showing up as a dot that goes out five minutes early.
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
