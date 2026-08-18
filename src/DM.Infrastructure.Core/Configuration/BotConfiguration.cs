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
    /// Secret for Telegram webhook URL validation.
    /// Sent by Telegram in X-Telegram-Bot-Api-Secret-Token when the webhook is
    /// registered with secret_token; the URL is /v1/webhooks/telegram.
    /// If not set, the webhook is closed: it answers 404 rather than accepting
    /// anything, because an unset secret is the shipped default.
    /// </summary>
    public string? TelegramWebhookSecret { get; set; }

    /// <summary>
    /// Public key of the Discord application, hex from Discord Developer Portal.
    /// Discord signs every interaction with the matching private key
    /// (X-Signature-Ed25519 over timestamp + raw body); the URL is
    /// /v1/webhooks/discord, registered as the Interactions Endpoint URL.
    /// If not set, the webhook is closed: it answers 404 rather than accepting
    /// anything, because an unset key is the shipped default.
    /// </summary>
    public string? DiscordPublicKey { get; set; }
}
