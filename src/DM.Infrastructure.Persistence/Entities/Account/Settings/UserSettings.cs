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

    /// <summary>
    /// A settings document for a user who has none yet, with a complete
    /// <see cref="Paging"/> sub-document.
    /// </summary>
    /// <remarks>
    /// The one place that answers "what does a fresh settings document look
    /// like". Three callers used to build it inline with three different
    /// answers, and one of them left Paging unset: the store has no schema, so
    /// the document persisted with <c>Paging: null</c>, after which every
    /// request from that user answered 500 (a null dereference during
    /// authentication) and every preferences update answered 500 as well —
    /// Mongo cannot create a field inside a null element.
    ///
    /// The numbers mirror <see cref="DM.Domain.Core.Identity.UserSettings.Default"/>,
    /// the values a caller without a document already sees.
    /// </remarks>
    public static UserSettings CreateDefault(Guid userId) => new()
    {
        UserId = userId,
        Theme = Theme.Light,
        Paging = new PagingSettings
        {
            TopicsPerPage = 10,
            CommentsPerPage = 10,
            PostsPerPage = 10,
            MessagesPerPage = 10,
            EntitiesPerPage = 10
        }
    };
}
