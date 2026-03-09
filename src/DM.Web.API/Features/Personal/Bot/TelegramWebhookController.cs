using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Features.Personal.Bot;

/// <summary>
/// Telegram webhook handler for bot commands
/// </summary>
[ApiController]
[Route("v1/bot/telegram")]
[ApiExplorerSettings(IgnoreApi = true)]
public class TelegramWebhookController : ControllerBase
{
    private readonly IBotLinkService _botLinkService;
    private readonly ILogger<TelegramWebhookController> _logger;

    /// <inheritdoc />
    public TelegramWebhookController(
        IBotLinkService botLinkService,
        ILogger<TelegramWebhookController> logger)
    {
        _botLinkService = botLinkService;
        _logger = logger;
    }

    /// <summary>
    /// Handle Telegram update (webhook)
    /// </summary>
    /// <param name="update">Telegram update object</param>
    /// <response code="200">Update processed</response>
    [HttpPost("webhook", Name = nameof(HandleUpdate))]
    [ProducesResponseType(200)]
    public async Task<IActionResult> HandleUpdate([FromBody] JsonElement update)
    {
        try
        {
            if (!update.TryGetProperty("message", out var message)) return Ok();
            if (!message.TryGetProperty("text", out var textElement)) return Ok();
            if (!message.TryGetProperty("chat", out var chat)) return Ok();
            if (!chat.TryGetProperty("id", out var chatId)) return Ok();

            var text = textElement.GetString();
            if (string.IsNullOrEmpty(text)) return Ok();

            var chatIdStr = chatId.GetRawText();

            if (text.StartsWith("/connect "))
            {
                var code = text["/connect ".Length..].Trim();
                var result = await _botLinkService.VerifyAndLink(code, "telegram", chatIdStr);
                // Bot response is handled by Telegram sender (or we could reply inline)
                _logger.LogInformation("Telegram /connect from chat {ChatId}: {Success}", chatIdStr, result.Success);
            }
            else if (text == "/start")
            {
                _logger.LogInformation("Telegram /start from chat {ChatId}", chatIdStr);
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "Error processing Telegram webhook update");
        }

        return Ok(); // Always return 200 to Telegram
    }
}
