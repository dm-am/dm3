using System;
using System.Collections.Generic;
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
    Task<IEnumerable<UserNotification>> GetAsync(PagingQuery query);

    #endregion

    #region Creating

    /// <summary>
    /// Create new notifications
    /// </summary>
    /// <param name="createNotifications">List of creating DTOs</param>
    Task<IEnumerable<CreateNotificationEntity>> CreateAsync(IEnumerable<CreateNotification> createNotifications);

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
