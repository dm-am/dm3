using System;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Account.Settings;

/// <summary>
/// DAL model for user settings: one row per user, keyed by the user.
/// </summary>
/// <remarks>
/// The page size is a NOT NULL column so a row is never partial: it exists
/// whole or not at all, and absence means <c>UserSettings.Default</c>.
/// </remarks>
[Table("UserSettings")]
public class UserSettings
{
    /// <summary>
    /// User identifier and primary key: one settings row per user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Website color theme
    /// </summary>
    public Theme Theme { get; set; }

    /// <summary>
    /// How many rows, posts, comments or messages a page of every paged list holds
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// IANA name of the reader's time zone. Null = UTC.
    /// </summary>
    public string? TimeZoneId { get; set; }

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

    /// <summary>
    /// A settings row for a user who has none yet.
    /// </summary>
    /// <remarks>
    /// The one place that answers "what does a fresh settings row look like".
    /// The values mirror <see cref="DM.Domain.Core.Identity.UserSettings.Default"/>,
    /// the values a caller without a row already sees.
    /// </remarks>
    public static UserSettings CreateDefault(Guid userId) => new()
    {
        UserId = userId,
        Theme = Theme.Light,
        PageSize = PagingPolicy.DefaultPageSize
    };
}
