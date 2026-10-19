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
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math.EC.Rfc8032;

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
/// verified against BotConfiguration.DiscordPublicKey. A missing or invalid
/// signature answers 401: Discord validates the endpoint by deliberately
/// sending broken signatures and expects exactly that code.
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

    private readonly IReadOnlyDictionary<string, IWebhookHandler> _handlers;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<WebhookController> _logger;

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
    /// <response code="200">Webhook processed; Discord reads the interaction response from the body</response>
    /// <response code="401">Invalid Discord signature</response>
    /// <response code="403">Invalid Telegram secret</response>
    /// <response code="404">Webhook is not configured for this type</response>
    [HttpPost("{type}")]
    [EnableRateLimiting(RateLimitPolicies.Sliding)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HandleWebhook(string type)
    {
        var normalizedType = type.ToLowerInvariant();

        // Read by hand rather than bound with [FromBody]: the Discord signature
        // covers the exact bytes on the wire, and a JsonElement re-serialized
        // after model binding is not those bytes.
        byte[] rawBody;
        using (var buffer = new MemoryStream())
        {
            await Request.Body.CopyToAsync(buffer);
            rawBody = buffer.ToArray();
        }

        switch (normalizedType)
        {
            case "telegram":
                {
                    // Fail closed: without a secret the webhook does not exist.
                    if (string.IsNullOrEmpty(_botConfig.TelegramWebhookSecret))
                    {
                        _logger.LogWarning(
                            "Webhook for {Type} received but no secret is configured; rejecting", type);
                        return NotFound();
                    }

                    // Fixed-time comparison: a plain != leaks the shared secret one byte at
                    // a time to anyone who can measure the response.
                    var provided = Encoding.UTF8.GetBytes(Request.Headers[TelegramSecretHeader].ToString());
                    var expected = Encoding.UTF8.GetBytes(_botConfig.TelegramWebhookSecret);
                    if (!CryptographicOperations.FixedTimeEquals(provided, expected))
                    {
                        _logger.LogWarning("Invalid webhook secret for {Type}", type);
                        // Thrown: ErrorHandlingMiddleware owns the body of every refusal.
                        throw new HttpException(HttpStatusCode.Forbidden, "Неверный секрет вебхука");
                    }

                    break;
                }
            case "discord":
                {
                    // Fail closed, as above.
                    if (string.IsNullOrEmpty(_botConfig.DiscordPublicKey))
                    {
                        _logger.LogWarning(
                            "Webhook for {Type} received but no public key is configured; rejecting", type);
                        return NotFound();
                    }

                    if (!VerifyDiscordSignature(
                            _botConfig.DiscordPublicKey,
                            Request.Headers[DiscordSignatureHeader].ToString(),
                            Request.Headers[DiscordTimestampHeader].ToString(),
                            rawBody))
                    {
                        _logger.LogWarning("Invalid Discord interaction signature");
                        // 401 by Discord's contract: endpoint validation sends
                        // deliberately broken signatures and requires this code.
                        throw new HttpException(HttpStatusCode.Unauthorized, "Неверная подпись запроса");
                    }

                    break;
                }
            default:
                // An unknown type answers the same as an unconfigured one:
                // a probe of the path learns nothing either way.
                return NotFound();
        }

        // The switch above lets through only the types a handler is registered for.
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
            _logger.LogError(ex, "Error processing {Type} webhook", type);
            return Ok();
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

    /// <summary>
    /// Verifies Discord's Ed25519 signature over timestamp + raw body.
    /// </summary>
    /// <remarks>
    /// The fixed-time rule the Telegram branch follows with FixedTimeEquals
    /// holds here by construction: Ed25519 verification does not branch on
    /// secret data. Malformed hex and wrong lengths are refusals, not errors —
    /// both arrive from anyone on the internet.
    /// </remarks>
    private static bool VerifyDiscordSignature(
        string publicKeyHex, string signatureHex, string timestamp, byte[] body)
    {
        if (string.IsNullOrEmpty(signatureHex) || string.IsNullOrEmpty(timestamp))
        {
            return false;
        }

        byte[] publicKey;
        byte[] signature;
        try
        {
            publicKey = Convert.FromHexString(publicKeyHex);
            signature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            return false;
        }

        if (publicKey.Length != Ed25519.PublicKeySize || signature.Length != Ed25519.SignatureSize)
        {
            return false;
        }

        var timestampBytes = Encoding.UTF8.GetBytes(timestamp);
        var signer = new Ed25519Signer();
        signer.Init(false, new Ed25519PublicKeyParameters(publicKey));
        signer.BlockUpdate(timestampBytes, 0, timestampBytes.Length);
        signer.BlockUpdate(body, 0, body.Length);
        return signer.VerifySignature(signature);
    }
}
