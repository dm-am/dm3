using System.Text.Json;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Personal.Notifications;

/// <summary>
/// Interface for bot webhook handlers
/// </summary>
public interface IWebhookHandler
{
    /// <summary>
    /// Bot type (telegram, discord)
    /// </summary>
    string BotType { get; }

    /// <summary>
    /// Handle incoming webhook payload.
    /// Returns the body of the 200 the provider expects back, or null when the
    /// 200 carries no body. Telegram reads nothing from the response; Discord
    /// reads the interaction response from it, and that response is the reply
    /// the invoking user sees.
    /// </summary>
    Task<object?> HandleAsync(JsonElement payload);
}
