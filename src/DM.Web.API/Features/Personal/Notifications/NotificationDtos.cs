using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Binding;

namespace DM.Web.API.Features.Personal.Notifications;

/// <summary>
/// DTO model for user notification
/// </summary>
public class Notification
{
    /// <summary>
    /// Notification identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Event type
    /// </summary>
    public EventType EventType { get; set; }

    /// <summary>
    /// When the notification was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Whether the reader has read it. A push is always unread: it is the moment
    /// the notification appears.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// What happened, in the words of the letter about the same event
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Path from the root of the site to what the notification is about, or null
    /// when there is nothing to lead to
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// The fields the letter prints under its title, less the time the
    /// notification already carries
    /// </summary>
    public IReadOnlyList<NotificationDetail> Details { get; set; } = [];

    /// <summary>
    /// Notification payload
    /// </summary>
    /// <remarks>
    /// The converter is what keeps the keys of this bag identical whether the DTO
    /// came off the bus or out of the store; see NotificationPayloadConverter.
    /// </remarks>
    [JsonConverter(typeof(NotificationPayloadConverter))]
    public object Payload { get; set; } = new { };
}

/// <summary>
/// One field of a notification under its title
/// </summary>
public class NotificationDetail
{
    /// <summary>
    /// What the field is called
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The value as the generator wrote it
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the value is a point in time, for the reader's clock to format like
    /// every date of the site
    /// </summary>
    public bool IsMoment { get; set; }
}

/// <summary>
/// DTO model for notification count
/// </summary>
public class NotificationCount
{
    /// <summary>
    /// Number of unread notifications
    /// </summary>
    public long Count { get; set; }
}

/// <summary>
/// Notification delivery settings for bot channels
/// </summary>
public class NotificationSettings
{
    /// <summary>
    /// Discord bot connection (null if not connected)
    /// </summary>
    public BotConnection? Discord { get; set; }

    /// <summary>
    /// Telegram bot connection (null if not connected)
    /// </summary>
    public BotConnection? Telegram { get; set; }
}

/// <summary>
/// Per-channel bot connection and notification settings
/// </summary>
public class BotConnection
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

/// <summary>
/// Result of generating a bot linking code
/// </summary>
public class BotLinkResult
{
    /// <summary>
    /// Verification code to send to the bot
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// When the code expires (UTC)
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; set; }
}

/// <summary>
/// Request to update notification settings for bot channels
/// </summary>
public class UpdateNotificationSettingsRequest
{
    /// <summary>
    /// Discord settings update (null = no change)
    /// </summary>
    public UpdateBotConnection? Discord { get; set; }

    /// <summary>
    /// Telegram settings update (null = no change)
    /// </summary>
    public UpdateBotConnection? Telegram { get; set; }
}

/// <summary>
/// Update for a single bot channel's settings
/// </summary>
public class UpdateBotConnection
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
