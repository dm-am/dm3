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
    /// Secret for Telegram webhook URL validation.
    /// Sent by Telegram in X-Telegram-Bot-Api-Secret-Token when the webhook is
    /// registered with secret_token; the URL is /v1/webhooks/telegram.
    /// If not set, any secret is accepted (dev mode).
    /// </summary>
    public string? TelegramWebhookSecret { get; set; }

    /// <summary>
    /// Secret for Discord webhook URL validation.
    /// Expected in X-Dm-Webhook-Secret; the URL is /v1/webhooks/discord.
    /// If not set, any secret is accepted (dev mode).
    /// </summary>
    public string? DiscordWebhookSecret { get; set; }
}
