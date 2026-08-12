using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Core.Enums;

namespace DM.Workers.NotificationDispatcher.Email;

/// <summary>
/// Sends notification emails to users based on their preferences
/// </summary>
public interface INotificationEmailSender
{
    /// <summary>
    /// Send notification email to interested users if their email preferences allow it
    /// </summary>
    /// <param name="notification">Created notification</param>
    /// <param name="eventType">Event type for category mapping</param>
    /// <param name="ct">Cancellation token</param>
    Task SendIfEnabled(CreateNotification notification, EventType eventType, CancellationToken ct = default);
}
