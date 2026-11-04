using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Entities.Personal.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc cref="INotificationRepository" />
internal class NotificationRepository : INotificationRepository
{
    /// <summary>
    /// The stored metadata is spelled the way the bus spells it (camelCase keys, enums
    /// as numbers), so the column travels to the reader verbatim, as the hub push does.
    /// </summary>
    private static readonly JsonSerializerOptions MetadataOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly DmDbContext _dbContext;

    public NotificationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<int> Count(Guid userId) => await _dbContext.NotificationRecipients
        .TagWith("DM.Notifications.Count")
        .CountAsync(r => r.UserId == userId);

    /// <inheritdoc />
    public async Task<long> CountUnread(Guid userId) => await _dbContext.NotificationRecipients
        .TagWith("DM.Notifications.CountUnread")
        .LongCountAsync(r => r.UserId == userId && !r.IsRead);

    /// <inheritdoc />
    public async Task<IEnumerable<ListedNotification>> GetNotifications(Guid userId, PagingData pagingData)
    {
        // The mark is the recipient row's, read alongside the notification it
        // joins: the list shows read and unread alike, and only this row knows
        // which one this reader is looking at.
        var rows = await _dbContext.NotificationRecipients
            .TagWith("DM.Notifications.Page")
            .Where(r => r.UserId == userId)
            .Join(_dbContext.Notifications,
                r => r.NotificationId,
                n => n.NotificationId,
                (r, n) => new { Notification = n, r.IsRead })
            .OrderByDescending(x => x.Notification.CreatedUtc)
            .ThenByDescending(x => x.Notification.NotificationId)
            .Skip(pagingData.Skip)
            .Take(pagingData.Take)
            .Select(x => new
            {
                x.Notification.NotificationId,
                x.Notification.EventType,
                x.Notification.CreatedUtc,
                x.Notification.Metadata,
                x.IsRead,
            })
            .ToListAsync();

        // Metadata leaves the store the way it went in: a document. The column
        // holds its serialized form, and handing the raw string up would make
        // every reader serialize it a second time, into a JSON string literal.
        return rows.Select(n => new ListedNotification
        {
            NotificationId = n.NotificationId,
            EventType = n.EventType,
            CreatedUtc = n.CreatedUtc,
            Metadata = JsonSerializer.Deserialize<JsonElement>(n.Metadata, MetadataOptions),
            IsRead = n.IsRead,
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<EventType>> GetCreatedEventTypes(Guid eventId)
    {
        var eventTypes = await _dbContext.Notifications
            .TagWith("DM.Notifications.CreatedForEvent")
            .Where(n => n.EventId == eventId)
            .Select(n => n.EventType)
            .ToListAsync();
        return eventTypes.ToHashSet();
    }

    /// <inheritdoc />
    public async Task Create(IEnumerable<CreateNotificationEntity> notifications)
    {
        _dbContext.Notifications.AddRange(notifications.Select(n => new Notification
        {
            NotificationId = n.NotificationId,
            CreatedUtc = n.CreatedUtc,
            EventType = n.EventType,
            EventId = n.EventId,
            Metadata = JsonSerializer.Serialize(n.Metadata, MetadataOptions),
            Recipients = n.UsersInterested
                .Distinct()
                .Select(userId => new NotificationRecipient
                {
                    NotificationId = n.NotificationId,
                    UserId = userId,
                    IsRead = false
                })
                .ToList()
        }));

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch
        {
            // The retry ladder replays the delivery on the same DbContext, and rows
            // left Added would collide with the (EventId, EventType) index.
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    // Idempotent UPDATE: a second "mark as read" changes nothing the first did not.

    /// <inheritdoc />
    public Task MarkAsRead(Guid notificationId, Guid userId) =>
        _dbContext.NotificationRecipients
            .Where(r => r.NotificationId == notificationId && r.UserId == userId && !r.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRead, true));

    /// <inheritdoc />
    public Task MarkAsRead(Guid userId) =>
        _dbContext.NotificationRecipients
            .Where(r => r.UserId == userId && !r.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRead, true));
}
