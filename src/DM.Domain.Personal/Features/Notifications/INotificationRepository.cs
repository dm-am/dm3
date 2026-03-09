using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Unified notification repository (MongoDB)
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
