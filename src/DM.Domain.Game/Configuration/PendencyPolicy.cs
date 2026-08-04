using System;

namespace DM.Domain.Game.Configuration;

/// <summary>
/// When a player is reminded that a room is waiting for their post.
/// </summary>
/// <remarks>
/// A product decision about how often the site is allowed to prod somebody, and
/// it used to be two fields of a background job in the HTTP host, where nothing
/// could read it, call it or test it without starting the host.
/// </remarks>
public static class PendencyPolicy
{
    /// <summary>
    /// How long a pendency goes unfulfilled before its first reminder.
    /// </summary>
    public static readonly TimeSpan FirstReminderAfter = TimeSpan.FromDays(3);

    /// <summary>
    /// Shortest gap between two reminders about the same pendency.
    /// </summary>
    public static readonly TimeSpan ReminderInterval = TimeSpan.FromDays(3);
}
