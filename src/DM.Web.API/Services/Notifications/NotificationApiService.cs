using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Notifications.BusinessProcesses.Flushing;
using DM.Services.Notifications.BusinessProcesses.Reading;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notifications;

namespace DM.Web.API.Services.Notifications;

/// <inheritdoc />
internal class NotificationApiService : INotificationApiService
{
    private readonly INotificationsReadingService _readingService;
    private readonly INotificationsFlushingService _flushingService;

    /// <inheritdoc />
    public NotificationApiService(
        INotificationsReadingService readingService,
        INotificationsFlushingService flushingService)
    {
        _readingService = readingService;
        _flushingService = flushingService;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Notification>> GetNotifications(int skip = 0, int take = 20)
    {
        var query = new PagingQuery { Skip = skip, Size = take };
        var notifications = await _readingService.Get(query);
        var mapped = notifications.Select(n => new Notification
        {
            Id = n.NotificationId,
            EventType = n.EventType,
            Payload = n.Metadata
        });

        return new ListEnvelope<Notification>(mapped);
    }

    /// <inheritdoc />
    public async Task<NotificationCount> GetUnreadCount()
    {
        var count = await _readingService.CountUnread();
        return new NotificationCount { Count = count };
    }

    /// <inheritdoc />
    public Task MarkAsRead(Guid notificationId) =>
        _flushingService.MarkAsRead(notificationId);

    /// <inheritdoc />
    public Task MarkAllAsRead() =>
        _flushingService.MarkAllAsRead();
}
