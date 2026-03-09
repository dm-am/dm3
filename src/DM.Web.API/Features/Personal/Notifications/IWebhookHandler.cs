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
    /// Handle incoming webhook payload
    /// </summary>
    Task HandleAsync(JsonElement payload);
}
