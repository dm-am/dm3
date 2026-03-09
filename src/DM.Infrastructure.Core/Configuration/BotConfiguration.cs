namespace DM.Infrastructure.Core.Configuration;

/// <summary>
/// Configuration for notification bots (Discord, Telegram).
/// Used for sending notifications, NOT for authentication.
/// </summary>
public class BotConfiguration
{
    /// <summary>
    /// Discord bot token for sending notifications (optional).
    /// Get it from Discord Developer Portal.
    /// </summary>
    public string? DiscordBotToken { get; set; }

    /// <summary>
    /// Telegram bot token for sending notifications (optional).
    /// Get it from @BotFather.
    /// </summary>
    public string? TelegramBotToken { get; set; }

    /// <summary>
    /// Base URL of the site for generating links in notifications.
    /// Defaults to https://dm.am if not configured.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Discord channel ID for moderation notifications (optional).
    /// Moderation events will be sent to this channel.
    /// </summary>
    public ulong? ModerationDiscordChannelId { get; set; }

    /// <summary>
    /// Telegram chat ID for moderation notifications (optional).
    /// Moderation events will be sent to this chat.
    /// </summary>
    public long? ModerationTelegramChatId { get; set; }

    /// <summary>
    /// API key for bot endpoints (optional).
    /// If set, bot endpoints will require X-Bot-Api-Key header with this value.
    /// If not set, bot endpoints are unprotected (dev mode).
    /// </summary>
    public string? BotApiKey { get; set; }

    /// <summary>
    /// Secret for Telegram webhook URL validation.
    /// The webhook URL will be /v1/webhooks/telegram/{secret}.
    /// If not set, any secret is accepted (dev mode).
    /// </summary>
    public string? TelegramWebhookSecret { get; set; }

    /// <summary>
    /// Secret for Discord webhook URL validation.
    /// The webhook URL will be /v1/webhooks/discord/{secret}.
    /// If not set, any secret is accepted (dev mode).
    /// </summary>
    public string? DiscordWebhookSecret { get; set; }
}
