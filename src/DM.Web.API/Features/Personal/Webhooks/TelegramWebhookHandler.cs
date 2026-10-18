using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Web.API.Features.Personal.Notifications;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Features.Personal.Webhooks;

/// <summary>
/// Handles Telegram bot updates delivered to the unified webhook endpoint
/// </summary>
/// <remarks>
/// Expects the standard Telegram Update object:
/// { "message": { "text": "...", "chat": { "id": 123 } } }.
///
/// Supported commands:
/// - "/connect {code}" - links the Telegram chat to a user account
/// - "/start {code}" - the same: what Telegram sends when the reader opens
///   the deep link t.me/{bot}?start={code} and presses Start in the chat
/// - "/start" - logged only
///
/// No replies are sent from here: outgoing bot messages are handled
/// by the notification dispatcher worker (NotificationBotSender).
/// </remarks>
internal class TelegramWebhookHandler : IWebhookHandler
{
    /// <summary>
    /// The commands that carry a link code after them. The account page hands
    /// the code out as a deep link, which arrives as "/start", and the command
    /// typed by hand stays "/connect".
    /// </summary>
    private static readonly string[] LinkCommandPrefixes = ["/connect ", "/start "];

    private readonly IBotLinkService _botLinkService;
    private readonly ILogger<TelegramWebhookHandler> _logger;

    /// <inheritdoc />
    public TelegramWebhookHandler(
        IBotLinkService botLinkService,
        ILogger<TelegramWebhookHandler> logger)
    {
        _botLinkService = botLinkService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string BotType => "telegram";

    /// <inheritdoc />
    public async Task<object?> HandleAsync(JsonElement payload)
    {
        if (!payload.TryGetProperty("message", out var message)) return null;
        if (!message.TryGetProperty("text", out var textElement)) return null;
        if (!message.TryGetProperty("chat", out var chat)) return null;
        if (!chat.TryGetProperty("id", out var chatId)) return null;

        var text = textElement.GetString();
        if (string.IsNullOrEmpty(text)) return null;

        var chatIdStr = chatId.GetRawText();

        var code = LinkCode(text);
        if (code != null)
        {
            var result = await _botLinkService.VerifyAndLink(code, BotType, chatIdStr);
            _logger.LogInformation("Telegram link code from chat {ChatId}: {Success}", chatIdStr, result.Success);
        }
        else if (text.Trim() == "/start")
        {
            _logger.LogInformation("Telegram /start from chat {ChatId}", chatIdStr);
        }

        return null;
    }

    /// <summary>
    /// The code after a link command, or null when the text is not one or names
    /// no code.
    /// </summary>
    private static string? LinkCode(string text)
    {
        foreach (var prefix in LinkCommandPrefixes)
        {
            if (!text.StartsWith(prefix))
            {
                continue;
            }

            var code = text[prefix.Length..].Trim();
            return code.Length == 0 ? null : code;
        }

        return null;
    }
}
