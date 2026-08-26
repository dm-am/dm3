using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account.Settings;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// Whether one channel of one reader carries one category.
/// </summary>
/// <remarks>
/// A reader with no settings row is a reader on the defaults, and every channel
/// the dispatcher sends through is opt-in, which is why an absent preference
/// answers no rather than yes.
/// </remarks>
internal static class NotificationChannels
{
    /// <summary>
    /// True when the channel is switched on and the category is one it carries.
    /// </summary>
    /// <param name="preferences">The reader's preferences for the channel, or null</param>
    /// <param name="category">Category the notification falls in</param>
    public static bool ShouldSend(NotificationChannelPreference? preferences, NotificationCategory category) =>
        preferences is { Enabled: true } && preferences.EnabledCategories.Contains(category);
}
