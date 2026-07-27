using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Configuration;
using DM.Web.API.Features.Personal.Notifications;
using Microsoft.AspNetCore.Http;
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
    /// <response code="404">Webhook secret is not configured for this type</response>
    [HttpPost("{type}/{secret}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        // Fail closed. An unconfigured secret used to mean "accept anything",
        // and an unconfigured secret is the shipped default — so the endpoint
        // was open to anyone who could guess the path. Without a secret the
        // webhook simply does not exist.
        if (string.IsNullOrEmpty(expectedSecret))
        {
            _logger.LogWarning(
                "Webhook for {Type} received but no secret is configured; rejecting", type);
            return NotFound();
        }

        // Fixed-time comparison: a plain != leaks the shared secret one byte at
        // a time to anyone who can measure the response.
        var provided = Encoding.UTF8.GetBytes(secret ?? string.Empty);
        var expected = Encoding.UTF8.GetBytes(expectedSecret);
        if (!CryptographicOperations.FixedTimeEquals(provided, expected))
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
