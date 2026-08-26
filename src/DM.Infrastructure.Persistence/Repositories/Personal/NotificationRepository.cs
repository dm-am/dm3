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
    /// The stored metadata is spelled the way the bus spells the same bag:
    /// camelCase keys, enums as numbers. One notification reaches one open tab
    /// twice — pushed over the hub as the JSON the dispatcher published, and
    /// read back from this table — and NotificationWireShould holds the two
    /// wires to one format. Storing the bus spelling is what lets the column
    /// travel to the reader verbatim (see NotificationPayloadConverter in the
    /// API, which copies a JsonElement through unchanged).
    /// </summary>
    private static readonly JsonSerializerOptions MetadataOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public NotificationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public async Task<long> Count(Guid userId) => await _dbContext.NotificationRecipients
        .TagWith("DM.Notifications.Count")
        .LongCountAsync(r => r.UserId == userId);

    /// <inheritdoc />
    public async Task<long> CountUnread(Guid userId) => await _dbContext.NotificationRecipients
        .TagWith("DM.Notifications.CountUnread")
        .LongCountAsync(r => r.UserId == userId && !r.IsRead);

    /// <inheritdoc />
    public async Task<IEnumerable<UserNotification>> GetNotifications(Guid userId, PagingData pagingData)
    {
        var rows = await _dbContext.NotificationRecipients
            .TagWith("DM.Notifications.Page")
            .Where(r => r.UserId == userId)
            .Join(_dbContext.Notifications,
                r => r.NotificationId,
                n => n.NotificationId,
                (r, n) => n)
            .OrderByDescending(n => n.CreatedUtc)
            .ThenByDescending(n => n.NotificationId)
            .Skip(pagingData.Skip)
            .Take(pagingData.Take)
            .Select(n => new
            {
                n.NotificationId,
                n.EventType,
                n.CreatedUtc,
                n.Metadata,
            })
            .ToListAsync();

        // Metadata leaves the store the way it went in: a document. The column
        // holds its serialized form, and handing the raw string up would make
        // every reader serialize it a second time, into a JSON string literal.
        return rows.Select(n => new UserNotification
        {
            NotificationId = n.NotificationId,
            EventType = n.EventType,
            CreatedUtc = n.CreatedUtc,
            Metadata = JsonSerializer.Deserialize<JsonElement>(n.Metadata, MetadataOptions),
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

    // ═══ WRITE ═══

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
            // The retry ladder replays the whole delivery inside the SAME scope
            // and the same DbContext (review of W1.4). A failed SaveChanges
            // leaves these rows Added, and replaying on top of them would
            // collide with the unique (EventId, EventType) index on the very
            // rows this attempt failed to commit - turning every transient
            // fault into a dead-lettered event. Clear, so the retry starts
            // from the lookup and a clean tracker, the way the neighbouring
            // repositories do.
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    // Idempotent UPDATE instead of the AddToSet guard: a second "mark as read"
    // arriving together with the first changes nothing the first did not.

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
