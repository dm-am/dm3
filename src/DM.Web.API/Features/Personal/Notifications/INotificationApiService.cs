using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Personal.Notifications;

/// <summary>
/// API service for notifications
/// </summary>
public interface INotificationApiService
{
    /// <summary>
    /// Get notifications for current user
    /// </summary>
    Task<IEnumerable<Notification>> GetNotifications(int skip = 0, int take = 20);

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
    Task MarkAsRead();

    /// <summary>
    /// Get notification settings for current user
    /// </summary>
    Task<NotificationSettings> GetNotificationSettings();

    /// <summary>
    /// Update notification settings for current user
    /// </summary>
    Task<NotificationSettings> UpdateNotificationSettings(UpdateNotificationSettingsRequest request);

    #region Bot Integration

    /// <summary>
    /// Generate a linking code for a notification bot
    /// </summary>
    /// <param name="type">Bot type (telegram, discord)</param>
    /// <returns>Linking code and expiration</returns>
    Task<BotLinkResult> ConnectBot(string type);

    /// <summary>
    /// Disconnect a notification bot
    /// </summary>
    /// <param name="type">Bot type (telegram, discord)</param>
    Task DisconnectBot(string type);

    #endregion
}
