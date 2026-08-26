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
    #region Reading

    /// <summary>
    /// Count unread notifications for current user
    /// </summary>
    Task<long> CountUnreadAsync();

    /// <summary>
    /// Get list of notifications for current user
    /// </summary>
    /// <param name="query">Paging query</param>
    Task<(IEnumerable<UserNotification> Notifications, PagingResult Paging)> GetAsync(PagingQuery query);

    #endregion

    #region Creating

    /// <summary>
    /// Create new notifications
    /// </summary>
    /// <remarks>
    /// Answers with the audience as it survived the recipient filter, not with
    /// the audience it was asked for. Every channel has to be built from this
    /// answer: the filter is what keeps a blocked person from reaching somebody,
    /// and a channel reading the request instead of the reply mails out exactly
    /// what the filter refused to store.
    ///
    /// Idempotent by (EventId, EventType): a request whose pair is already
    /// stored is dropped from the answer and stored again by nobody, which is
    /// what makes a redelivered bus event write once and mail once. A request
    /// without an EventId is created unconditionally, as before the key existed.
    /// </remarks>
    /// <param name="createNotifications">List of creating DTOs</param>
    /// <param name="ct">Cancellation token</param>
    Task<IReadOnlyList<CreatedNotification>> CreateAsync(
        IEnumerable<CreateNotification> createNotifications, CancellationToken ct = default);

    #endregion

    #region Flushing

    /// <summary>
    /// Mark single notification as read by current user
    /// </summary>
    /// <param name="notificationId">Notification identifier</param>
    Task MarkAsReadAsync(Guid notificationId);

    /// <summary>
    /// Mark all notifications as read for current user
    /// </summary>
    Task MarkAllAsReadAsync();

    #endregion
}
