using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Account.Settings;

/// <summary>
/// Per-channel notification delivery preferences (Discord or Telegram).
/// Controls WHERE to deliver notifications for a specific channel.
///
/// <para><b>Notification Delivery Logic:</b></para>
/// <list type="bullet">
///   <item><description>SubscriptionSettings: controls WHAT to notify about (per subscription)</description></item>
///   <item><description>NotificationChannelPreferences: controls WHERE to deliver (per channel)</description></item>
/// </list>
///
/// <para><b>Final notification = SubscriptionSettings ∩ ChannelPreferences:</b></para>
/// <list type="number">
///   <item><description>Event occurs and SubscriptionSettings flag is set</description></item>
///   <item><description>If Enabled=false → skip this channel</description></item>
///   <item><description>If EnabledCategories doesn't include event's category → skip</description></item>
///   <item><description>Otherwise → deliver to this channel</description></item>
/// </list>
/// </summary>
public class NotificationChannelPreference
{
    /// <summary>
    /// Global on/off for this channel. User can pause all bot notifications.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Which notification categories to deliver via this channel.
    /// Default: Messages + Games + Security.
    /// </summary>
    public HashSet<NotificationCategory> EnabledCategories { get; set; } = new()
    {
        NotificationCategory.Messages,
        NotificationCategory.Games,
        NotificationCategory.Security
    };
}
