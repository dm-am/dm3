using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notifications;

namespace DM.Web.API.Services.Notifications;

/// <summary>
/// API service for notifications
/// </summary>
public interface INotificationApiService
{
    /// <summary>
    /// Get notifications for current user
    /// </summary>
    Task<ListEnvelope<Notification>> GetNotifications(int skip = 0, int take = 20);

    /// <summary>
    /// Get unread notifications count
    /// </summary>
    Task<NotificationCount> GetUnreadCount();

    /// <summary>
    /// Mark notification as read
    /// </summary>
    Task MarkAsRead(Guid notificationId);

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    Task MarkAllAsRead();
}
