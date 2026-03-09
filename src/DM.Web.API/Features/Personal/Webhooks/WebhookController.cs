using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Configuration;
using DM.Web.API.Features.Personal.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Features.Personal.Webhooks;

/// <summary>
/// Unified webhook handler for external bot callbacks
/// </summary>
/// <remarks>
/// Handles incoming webhooks from Telegram and Discord bots.
/// Each webhook URL includes a secret for validation.
///
/// ## Telegram Setup
/// Set webhook URL to: POST /v1/webhooks/telegram/{TelegramWebhookSecret}
///
/// ## Discord Setup
/// Set webhook URL to: POST /v1/webhooks/discord/{DiscordWebhookSecret}
///
/// The bot parses /connect {code} commands and links user accounts.
/// </remarks>
[ApiController]
[Route("v1/webhooks")]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebhookController : ControllerBase
{
    private readonly IReadOnlyDictionary<string, IWebhookHandler> _handlers;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<WebhookController> _logger;

    /// <inheritdoc />
    public WebhookController(
        IEnumerable<IWebhookHandler> handlers,
        IOptions<BotConfiguration> botConfig,
        ILogger<WebhookController> logger)
    {
        _handlers = handlers.ToDictionary(h => h.BotType, h => h);
        _botConfig = botConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Handle webhook from Telegram or Discord bot
    /// </summary>
    /// <param name="type">Bot type: telegram or discord</param>
    /// <param name="secret">Webhook secret for validation</param>
    /// <param name="payload">Raw webhook payload</param>
    /// <response code="200">Webhook processed</response>
    /// <response code="403">Invalid secret</response>
    [HttpPost("{type}/{secret}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> HandleWebhook(string type, string secret, [FromBody] JsonElement payload)
    {
        var normalizedType = type.ToLowerInvariant();

        // Validate secret
        var expectedSecret = normalizedType switch
        {
            "telegram" => _botConfig.TelegramWebhookSecret,
            "discord" => _botConfig.DiscordWebhookSecret,
            _ => null
        };

        // In dev mode (no secret configured), accept any request
        if (!string.IsNullOrEmpty(expectedSecret) && secret != expectedSecret)
        {
            _logger.LogWarning("Invalid webhook secret for {Type}", type);
            return StatusCode(403, new { error = "Invalid webhook secret" });
        }

        // Find handler
        if (!_handlers.TryGetValue(normalizedType, out var handler))
        {
            return BadRequest(new { error = $"Unknown webhook type: {type}" });
        }

        try
        {
            await handler.HandleAsync(payload);
            return Ok();
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "Error processing {Type} webhook", type);
            return Ok(); // Always return 200 to prevent retries
        }
    }
}
