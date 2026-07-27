using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Web.API.Features.Personal.Notifications;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Features.Personal.Webhooks;

/// <summary>
/// Handles Discord bot messages delivered to the unified webhook endpoint
/// </summary>
/// <remarks>
/// Discord does not push direct messages over plain HTTP webhooks, so a
/// relay (a gateway worker or an interactions endpoint) has to forward
/// message events to this endpoint. There is no established inbound
/// Discord contract in this codebase yet; this handler expects a minimal
/// MESSAGE_CREATE-shaped JSON:
/// { "content": "/connect ABC123", "author": { "id": "1234567890" } }.
///
/// Supported commands:
/// - "/connect {code}" - links the Discord account to a user account
///
/// No replies are sent from here: outgoing bot messages are handled
/// by the notification dispatcher worker (NotificationBotSender).
/// </remarks>
internal class DiscordWebhookHandler : IWebhookHandler
{
    private const string ConnectCommandPrefix = "/connect ";

    private readonly IBotLinkService _botLinkService;
    private readonly ILogger<DiscordWebhookHandler> _logger;

    /// <inheritdoc />
    public DiscordWebhookHandler(
        IBotLinkService botLinkService,
        ILogger<DiscordWebhookHandler> logger)
    {
        _botLinkService = botLinkService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string BotType => "discord";

    /// <inheritdoc />
    public async Task HandleAsync(JsonElement payload)
    {
        if (!payload.TryGetProperty("content", out var contentElement)) return;
        if (!payload.TryGetProperty("author", out var author)) return;
        if (!author.TryGetProperty("id", out var authorId)) return;

        var text = contentElement.GetString();
        if (string.IsNullOrEmpty(text)) return;

        // Author id is a snowflake; gateway payloads carry it as a string,
        // but accept a numeric value as well just in case
        var externalId = authorId.ValueKind == JsonValueKind.String
            ? authorId.GetString()
            : authorId.GetRawText();
        if (string.IsNullOrEmpty(externalId)) return;

        if (text.StartsWith(ConnectCommandPrefix))
        {
            var code = text[ConnectCommandPrefix.Length..].Trim();
            var result = await _botLinkService.VerifyAndLink(code, BotType, externalId);
            _logger.LogInformation("Discord /connect from user {ExternalId}: {Success}", externalId, result.Success);
        }
    }
}
