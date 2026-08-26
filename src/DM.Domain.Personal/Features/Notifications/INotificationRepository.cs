using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Unified notification repository
/// </summary>
public interface INotificationRepository
{
    // ═══ READ ═══

    /// <summary>
    /// Counts user notifications
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>All notifications count</returns>
    Task<long> Count(Guid userId);

    /// <summary>
    /// Counts unread notifications for user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Unread notifications count</returns>
    Task<long> CountUnread(Guid userId);

    /// <summary>
    /// Gets paged notifications for user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="pagingData">Paging data</param>
    /// <returns>List of notifications ordered by invocation date</returns>
    Task<IEnumerable<UserNotification>> GetNotifications(Guid userId, PagingData pagingData);

    /// <summary>
    /// Event types of the notifications already stored for a bus publication
    /// </summary>
    /// <remarks>
    /// What the idempotent write reads before creating: a redelivered event finds
    /// here what its first delivery stored, one type per generator that answered.
    /// </remarks>
    /// <param name="eventId">Bus publication identifier</param>
    /// <returns>Event types stored under the identifier</returns>
    Task<IReadOnlySet<EventType>> GetCreatedEventTypes(Guid eventId);

    // ═══ WRITE ═══

    /// <summary>
    /// Create new notifications
    /// </summary>
    /// <param name="notifications">Notification entity DTOs</param>
    Task Create(IEnumerable<CreateNotificationEntity> notifications);

    /// <summary>
    /// Mark single notification as read by user
    /// </summary>
    /// <param name="notificationId">Notification identifier</param>
    /// <param name="userId">User identifier</param>
    Task MarkAsRead(Guid notificationId, Guid userId);

    /// <summary>
    /// Mark all user notifications as read
    /// </summary>
    /// <param name="userId">User identifier</param>
    Task MarkAsRead(Guid userId);
}
