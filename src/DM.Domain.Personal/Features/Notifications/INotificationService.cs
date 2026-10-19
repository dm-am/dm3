using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Unified service for notification operations
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Count unread notifications for current user
    /// </summary>
    Task<long> CountUnreadAsync();

    /// <summary>
    /// Get list of notifications for current user
    /// </summary>
    /// <param name="query">Paging query</param>
    Task<(IEnumerable<ListedNotification> Notifications, PagingResult Paging)> GetAsync(PagingQuery query);

    /// <summary>
    /// Create new notifications
    /// </summary>
    /// <remarks>
    /// Answers with the audience as it survived the recipient filter: every channel
    /// is built from this answer. Idempotent by (EventId, EventType), so a
    /// redelivered bus event writes once and mails once; a request without an
    /// EventId is created unconditionally.
    /// </remarks>
    /// <param name="createNotifications">List of creating DTOs</param>
    /// <param name="ct">Cancellation token</param>
    Task<IReadOnlyList<CreatedNotification>> CreateAsync(
        IEnumerable<CreateNotification> createNotifications, CancellationToken ct = default);

    /// <summary>
    /// Mark single notification as read by current user
    /// </summary>
    /// <param name="notificationId">Notification identifier</param>
    Task MarkAsReadAsync(Guid notificationId);

    /// <summary>
    /// Mark all notifications as read for current user
    /// </summary>
    Task MarkAllAsReadAsync();
}
