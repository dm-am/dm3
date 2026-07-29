using System;
using System.ComponentModel.DataAnnotations;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Bson.Serialization.Attributes;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Account.Settings;

/// <summary>
/// DAL model for user settings
/// </summary>
[MongoCollectionName("UserSettings")]
[BsonIgnoreExtraElements]
public class UserSettings
{
    /// <summary>
    /// User identifier
    /// </summary>
    [Key]
    public Guid UserId { get; set; }

    /// <summary>
    /// Paging settings
    /// </summary>
    public PagingSettings Paging { get; set; } = null!;

    /// <summary>
    /// Website color theme
    /// </summary>
    public Theme Theme { get; set; }

    /// <summary>
    /// Discord notification channel preferences. Null = channel not connected.
    /// </summary>
    public NotificationChannelPreference? DiscordPreferences { get; set; }

    /// <summary>
    /// Telegram notification channel preferences. Null = channel not connected.
    /// </summary>
    public NotificationChannelPreference? TelegramPreferences { get; set; }

    /// <summary>
    /// Email notification channel preferences.
    /// Unlike Discord/Telegram, email is always "connected" via user's registered email.
    /// Null = use defaults (disabled by default to avoid spam).
    /// </summary>
    public NotificationChannelPreference? EmailPreferences { get; set; }
}
