using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Web.API.Features.Personal.Notifications;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Features.Personal.Webhooks;

/// <summary>
/// Handles Discord interactions delivered to the unified webhook endpoint
/// </summary>
/// <remarks>
/// The endpoint is the Interactions Endpoint URL of the Discord application:
/// Discord itself calls it with a signed request for every PING (type 1) and
/// every slash command (type 2). The interaction response returned from here
/// is the reply the invoking user sees, so unlike Telegram this handler does
/// answer: the reply is ephemeral (flags 64), visible to the invoker alone,
/// because a linking code is that user's business and nobody else's.
///
/// Supported commands:
/// - "/connect {code}" - links the Discord account to a user account
/// </remarks>
internal class DiscordWebhookHandler : IWebhookHandler
{
    private const string ConnectCommandName = "connect";
    private const string CodeOptionName = "code";

    /// <summary>Interaction types Discord delivers here.</summary>
    private const int InteractionPing = 1;

    /// <inheritdoc cref="InteractionPing" />
    private const int InteractionApplicationCommand = 2;

    /// <summary>Interaction callback types this handler answers with.</summary>
    private const int CallbackPong = 1;

    /// <inheritdoc cref="CallbackPong" />
    private const int CallbackChannelMessage = 4;

    /// <summary>MessageFlags.Ephemeral: the reply is shown to the invoker alone.</summary>
    private const int EphemeralFlag = 64;

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
    public async Task<object?> HandleAsync(JsonElement payload)
    {
        if (!payload.TryGetProperty("type", out var typeElement) ||
            !typeElement.TryGetInt32(out var interactionType))
        {
            return null;
        }

        return interactionType switch
        {
            InteractionPing => new InteractionResponse { Type = CallbackPong },
            InteractionApplicationCommand => await HandleCommand(payload),
            // Only the types above can arrive: the application registers no
            // components, no modals and no autocomplete to produce the others.
            _ => null
        };
    }

    /// <summary>
    /// The slash command branch. Every type 2 gets an interaction response:
    /// left unanswered, Discord shows the invoker a failure of its own wording.
    /// </summary>
    private async Task<object?> HandleCommand(JsonElement payload)
    {
        if (!payload.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("name", out var nameElement) ||
            nameElement.GetString() != ConnectCommandName)
        {
            return Ephemeral("Такой команды нет. Для привязки аккаунта отправьте /connect с кодом из настроек уведомлений на сайте.");
        }

        var externalId = ResolveInvokerId(payload);
        if (string.IsNullOrEmpty(externalId))
        {
            return Ephemeral("Не удалось определить отправителя команды.");
        }

        var code = StringOption(data, CodeOptionName)?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            return Ephemeral("В команде нет кода. Возьмите его в настройках уведомлений на сайте.");
        }

        var result = await _botLinkService.VerifyAndLink(code, BotType, externalId);
        _logger.LogInformation("Discord /connect from user {ExternalId}: {Success}", externalId, result.Success);

        return Ephemeral(result.Success
            ? $"Аккаунт привязан к {result.Username}. Уведомления будут приходить сюда."
            : "Код не подошел: он неверный или истек. Возьмите новый в настройках уведомлений на сайте.");
    }

    /// <summary>
    /// Id of the user who invoked the command. Inside a guild the user rides in
    /// "member", in a DM at the top level; both shapes are Discord's own.
    /// </summary>
    private static string? ResolveInvokerId(JsonElement payload)
    {
        var user = payload.TryGetProperty("member", out var member) &&
                   member.TryGetProperty("user", out var memberUser)
            ? memberUser
            : payload.TryGetProperty("user", out var directUser)
                ? directUser
                : default;

        if (user.ValueKind != JsonValueKind.Object ||
            !user.TryGetProperty("id", out var id))
        {
            return null;
        }

        // A snowflake travels as a string in interactions; a numeric value is
        // accepted as well just in case
        return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
    }

    /// <summary>String value of a named command option.</summary>
    private static string? StringOption(JsonElement data, string name)
    {
        if (!data.TryGetProperty("options", out var options) ||
            options.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var option in options.EnumerateArray())
        {
            if (option.TryGetProperty("name", out var optionName) &&
                optionName.GetString() == name &&
                option.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static InteractionResponse Ephemeral(string content) => new()
    {
        Type = CallbackChannelMessage,
        Data = new InteractionCallbackData { Content = content, Flags = EphemeralFlag }
    };

    /// <summary>
    /// Interaction response as Discord defines it. Property names are pinned
    /// with attributes rather than left to the host's naming policy: this is
    /// Discord's contract, not this API's.
    /// </summary>
    private sealed class InteractionResponse
    {
        [JsonPropertyName("type")]
        public int Type { get; init; }

        [JsonPropertyName("data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public InteractionCallbackData? Data { get; init; }
    }

    /// <inheritdoc cref="InteractionResponse" />
    private sealed class InteractionCallbackData
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;

        [JsonPropertyName("flags")]
        public int Flags { get; init; }
    }
}
