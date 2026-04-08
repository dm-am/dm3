using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

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
    /// Notification payload
    /// </summary>
    public object Payload { get; set; } = new { };
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

/// <summary>
/// Channels for delivering notifications
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// Email notifications (always available)
    /// </summary>
    Email = 1,

    /// <summary>
    /// Telegram bot notifications (requires connection)
    /// </summary>
    Telegram = 2,

    /// <summary>
    /// Discord bot notifications (requires connection)
    /// </summary>
    Discord = 3
}

/// <summary>
/// Request to verify a bot linking code (called by bot backend)
/// </summary>
public class VerifyBotLinkRequest
{
    /// <summary>
    /// Verification code entered by user in the bot
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Channel type (telegram or discord)
    /// </summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// External user ID from the bot platform
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;
}
