using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Notifications;

/// <summary>
/// Request to update notification preferences for a channel
/// </summary>
public class UpdatePreferencesRequest
{
    /// <summary>
    /// Discord preferences update (null = no change)
    /// </summary>
    public UpdateChannelPreferences? Discord { get; set; }

    /// <summary>
    /// Telegram preferences update (null = no change)
    /// </summary>
    public UpdateChannelPreferences? Telegram { get; set; }
}

/// <summary>
/// Update for a single channel's preferences
/// </summary>
public class UpdateChannelPreferences
{
    /// <summary>
    /// Whether notifications are enabled
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Updated set of enabled categories (replaces existing)
    /// </summary>
    public HashSet<NotificationCategory>? EnabledCategories { get; set; }
}
