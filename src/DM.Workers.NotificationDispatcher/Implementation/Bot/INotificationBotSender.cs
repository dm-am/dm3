using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Core.Enums;

namespace DM.Workers.NotificationDispatcher.Implementation.Bot;

/// <summary>
/// Sends notifications to Discord/Telegram bots based on user preferences
/// </summary>
public interface INotificationBotSender
{
    /// <summary>
    /// Send notification to Discord/Telegram if user preferences allow it
    /// </summary>
    /// <param name="notification">Created notification</param>
    /// <param name="eventType">Event type for category mapping</param>
    /// <param name="ct">Cancellation token</param>
    Task SendIfEnabled(CreateNotification notification, EventType eventType, CancellationToken ct = default);
}
