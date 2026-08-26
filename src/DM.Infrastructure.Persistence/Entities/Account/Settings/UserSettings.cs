using System;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Account.Settings;

/// <summary>
/// DAL model for user settings: one row per user, keyed by the user.
/// </summary>
/// <remarks>
/// The paging numbers are NOT NULL columns rather than a nested document on
/// purpose: the store used to hold a settings document with <c>Paging: null</c>,
/// and every request from that user answered 500 until the document was
/// repaired. Columns make that state unrepresentable — a row either exists whole
/// or does not exist at all, and absence means <c>UserSettings.Default</c>.
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
    /// Number of detached topics on a forum page
    /// </summary>
    public int TopicsPerPage { get; set; }

    /// <summary>
    /// Number of comments on a game or a topic page
    /// </summary>
    public int CommentsPerPage { get; set; }

    /// <summary>
    /// Number of posts on a game room page
    /// </summary>
    public int PostsPerPage { get; set; }

    /// <summary>
    /// Number of private messages and conversations on dialogue page
    /// </summary>
    public int MessagesPerPage { get; set; }

    /// <summary>
    /// Number of other entities on a single page
    /// </summary>
    public int EntitiesPerPage { get; set; }

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
    /// The numbers mirror <see cref="DM.Domain.Core.Identity.UserSettings.Default"/>,
    /// the values a caller without a row already sees.
    /// </remarks>
    public static UserSettings CreateDefault(Guid userId) => new()
    {
        UserId = userId,
        Theme = Theme.Light,
        TopicsPerPage = 10,
        CommentsPerPage = 10,
        PostsPerPage = 10,
        MessagesPerPage = 10,
        EntitiesPerPage = 10
    };
}
