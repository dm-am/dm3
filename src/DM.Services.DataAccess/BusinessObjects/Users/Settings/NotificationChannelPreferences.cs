using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace DM.Services.DataAccess.BusinessObjects.Users.Settings;

/// <summary>
/// Per-channel notification delivery preferences (Discord or Telegram)
/// </summary>
[BsonIgnoreExtraElements]
public class NotificationChannelPreferences
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
