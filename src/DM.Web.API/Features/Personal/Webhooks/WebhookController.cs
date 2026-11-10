using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Core.Configuration;
using DM.Web.API.Features.Personal.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DM.Web.API.Shared.RateLimiting;

namespace DM.Web.API.Features.Personal.Webhooks;

/// <summary>
/// Unified webhook handler for external bot callbacks
/// </summary>
/// <remarks>
/// Handles incoming webhooks from Telegram and Discord bots.
///
/// Credentials travel in headers, never in the path, which every proxy log and
/// trace writes verbatim.
///
/// ## Telegram
/// POST /v1/webhooks/telegram, secret in `X-Telegram-Bot-Api-Secret-Token`:
/// Telegram's own mechanism, it sends the header on every call.
///
/// ## Discord
/// POST /v1/webhooks/discord. There is no shared secret: Discord signs every
/// request with the application's Ed25519 key — `X-Signature-Ed25519` over the
/// UTF-8 bytes of `X-Signature-Timestamp` + raw body — and the signature is
/// verified against BotConfiguration.DiscordPublicKey by DiscordSignatureVerifier.
/// A missing, invalid or stale signature answers 401: Discord validates the
/// endpoint by deliberately sending broken signatures and expects exactly that
/// code.
///
/// The /connect slash command links user accounts; the interaction response
/// returned in the 200 is the reply the invoking user sees.
///
/// Registering the webhooks with the bots: docs/references/CONFIGURATION.md,
/// "Боты уведомлений".
/// </remarks>
[ApiController]
[Route("v1/webhooks")]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebhookController : ControllerBase
{
    /// <summary>Telegram's own secret header, sent when the webhook is registered with secret_token.</summary>
    private const string TelegramSecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    /// <summary>Discord's signature pair, sent with every interaction.</summary>
    private const string DiscordSignatureHeader = "X-Signature-Ed25519";

    /// <inheritdoc cref="DiscordSignatureHeader" />
    private const string DiscordTimestampHeader = "X-Signature-Timestamp";

    /// <summary>Ceiling of a webhook body.</summary>
    /// <remarks>
    /// The body is buffered whole ahead of any credential check, and an update of either provider is
    /// kilobytes: under the host's ceiling an anonymous caller fills megabytes of memory per request.
    /// </remarks>
    internal const int MaxBodyBytes = 256 * 1024;

    private readonly IReadOnlyDictionary<string, IWebhookHandler> _handlers;
    private readonly BotConfiguration _botConfig;
    private readonly DiscordSignatureVerifier _discordSignature;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IEnumerable<IWebhookHandler> handlers,
        IOptions<BotConfiguration> botConfig,
        DiscordSignatureVerifier discordSignature,
        ILogger<WebhookController> logger)
    {
        _handlers = handlers.ToDictionary(h => h.BotType, h => h);
        _botConfig = botConfig.Value;
        _discordSignature = discordSignature;
        _logger = logger;
    }

    /// <summary>
    /// Handle webhook from Telegram or Discord bot
    /// </summary>
    /// <param name="type">Bot type: telegram or discord</param>
    /// <response code="200">Webhook processed; Discord reads the interaction response from the body</response>
    /// <response code="401">Invalid Discord signature, or its timestamp is far from the server's clock</response>
    /// <response code="403">Invalid Telegram secret</response>
    /// <response code="404">Webhook is not configured for this type</response>
    /// <response code="413">Body larger than any update of a provider</response>
    [HttpPost("{type}")]
    [EnableRateLimiting(RateLimitPolicies.Sliding)]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> HandleWebhook(string type)
    {
        var normalizedType = type.ToLowerInvariant();
        var rawBody = await ReadRawBody();

        if (!Authenticate(normalizedType, rawBody))
        {
            return NotFound();
        }

        // Authenticate lets through only the types a handler is registered for.
        var handler = _handlers[normalizedType];

        // Parsed only behind the checks: a caller who failed them gets a refusal.
        using var document = ParseBody(rawBody);

        try
        {
            var response = await handler.HandleAsync(document.RootElement);
            return response == null ? Ok() : Ok(response);
        }
        catch (Exception ex)
        {
            // 200 keeps the provider from retrying a payload we already failed on.
            _logger.LogError(ex, "Error processing {Type} webhook", handler.BotType);
            return Ok();
        }
    }

    /// <remarks>
    /// Read by hand rather than bound with [FromBody]: the Discord signature
    /// covers the exact bytes on the wire, and a JsonElement re-serialized
    /// after model binding is not those bytes.
    /// </remarks>
    private async Task<byte[]> ReadRawBody()
    {
        using var buffer = new MemoryStream();
        try
        {
            await Request.Body.CopyToAsync(buffer);
        }
        catch (BadHttpRequestException refusal) when (refusal.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // The server stops the read at MaxBodyBytes with an exception of its own, which
            // ErrorHandlingMiddleware would answer as an unhandled failure.
            throw new HttpException(HttpStatusCode.RequestEntityTooLarge, "Тело запроса больше допустимого");
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Proves the caller is the provider of the bot: every credential check of the endpoint, in one place.
    /// </summary>
    /// <remarks>
    /// False when there is no such webhook here: an unknown type, or one with no secret or key configured.
    /// </remarks>
    private bool Authenticate(string botType, byte[] rawBody)
    {
        switch (botType)
        {
            case "telegram":
                {
                    // Fail closed: without a secret the webhook does not exist.
                    if (string.IsNullOrEmpty(_botConfig.TelegramWebhookSecret))
                    {
                        _logger.LogWarning("Telegram webhook received but no secret is configured; rejecting");
                        return false;
                    }

                    // Fixed-time comparison: a plain != leaks the shared secret one byte at
                    // a time to anyone who can measure the response.
                    var provided = Encoding.UTF8.GetBytes(Request.Headers[TelegramSecretHeader].ToString());
                    var expected = Encoding.UTF8.GetBytes(_botConfig.TelegramWebhookSecret);
                    if (!CryptographicOperations.FixedTimeEquals(provided, expected))
                    {
                        _logger.LogWarning("Invalid Telegram webhook secret");
                        // Thrown: ErrorHandlingMiddleware owns the body of every refusal.
                        throw new HttpException(HttpStatusCode.Forbidden, "Неверный секрет вебхука");
                    }

                    return true;
                }
            case "discord":
                {
                    // Fail closed, as above.
                    if (string.IsNullOrEmpty(_botConfig.DiscordPublicKey))
                    {
                        _logger.LogWarning("Discord webhook received but no public key is configured; rejecting");
                        return false;
                    }

                    if (!_discordSignature.Verify(
                            _botConfig.DiscordPublicKey,
                            Request.Headers[DiscordSignatureHeader].ToString(),
                            Request.Headers[DiscordTimestampHeader].ToString(),
                            rawBody))
                    {
                        // 401 by Discord's contract: endpoint validation sends
                        // deliberately broken signatures and requires this code.
                        throw new HttpException(HttpStatusCode.Unauthorized, "Неверная подпись запроса");
                    }

                    return true;
                }
            default:
                // An unknown type answers the same as an unconfigured one:
                // a probe of the path learns nothing either way.
                return false;
        }
    }

    private static JsonDocument ParseBody(byte[] rawBody)
    {
        try
        {
            return JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            // Thrown: ErrorHandlingMiddleware owns the body of the refusal.
            throw new HttpException(HttpStatusCode.BadRequest, "Тело запроса не является корректным JSON");
        }
    }
}
