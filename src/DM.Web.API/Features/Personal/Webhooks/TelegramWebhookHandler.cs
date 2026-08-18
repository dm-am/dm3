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
/// - "/start" - logged only
///
/// No replies are sent from here: outgoing bot messages are handled
/// by the notification dispatcher worker (NotificationBotSender).
/// </remarks>
internal class TelegramWebhookHandler : IWebhookHandler
{
    private const string ConnectCommandPrefix = "/connect ";

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

        if (text.StartsWith(ConnectCommandPrefix))
        {
            var code = text[ConnectCommandPrefix.Length..].Trim();
            var result = await _botLinkService.VerifyAndLink(code, BotType, chatIdStr);
            _logger.LogInformation("Telegram /connect from chat {ChatId}: {Success}", chatIdStr, result.Success);
        }
        else if (text == "/start")
        {
            _logger.LogInformation("Telegram /start from chat {ChatId}", chatIdStr);
        }

        return null;
    }
}
