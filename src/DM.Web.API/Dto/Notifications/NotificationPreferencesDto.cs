using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Notifications;

/// <summary>
/// Notification delivery preferences for bot channels
/// </summary>
public class NotificationPreferencesDto
{
    /// <summary>
    /// Discord channel preferences (null if not connected)
    /// </summary>
    public ChannelPreferencesDto? Discord { get; set; }

    /// <summary>
    /// Telegram channel preferences (null if not connected)
    /// </summary>
    public ChannelPreferencesDto? Telegram { get; set; }
}

/// <summary>
/// Per-channel notification preferences
/// </summary>
public class ChannelPreferencesDto
{
    /// <summary>
    /// Whether the channel is connected to the account
    /// </summary>
    public bool Connected { get; set; }

    /// <summary>
    /// Whether notifications are enabled for this channel
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Notification categories enabled for this channel
    /// </summary>
    public HashSet<NotificationCategory> EnabledCategories { get; set; } = new();
}
