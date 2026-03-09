using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Entities.Notifications;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Driver;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc cref="INotificationRepository" />
internal class NotificationRepository : MongoCollectionRepository<Notification>, INotificationRepository
{
    /// <inheritdoc />
    public NotificationRepository(DmMongoClient client) : base(client)
    {
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<long> Count(Guid userId) => Collection.CountDocumentsAsync(UserInterested(userId));

    /// <inheritdoc />
    public Task<long> CountUnread(Guid userId) => Collection.CountDocumentsAsync(UserToBeNotified(userId));

    /// <inheritdoc />
    public async Task<IEnumerable<UserNotification>> GetNotifications(Guid userId, PagingData pagingData)
    {
        return await Collection
            .Find(UserInterested(userId))
            .Sort(Sort.Descending(n => n.CreatedUtc))
            .Skip(pagingData.Skip)
            .Limit(pagingData.Take)
            .Project(Project.As<UserNotification>())
            .ToListAsync();
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public Task Create(IEnumerable<CreateNotificationEntity> notifications)
    {
        var entities = notifications.Select(n => new Notification
        {
            NotificationId = n.NotificationId,
            CreatedUtc = n.CreatedUtc.DateTime,
            UsersInterested = n.UsersInterested,
            UsersNotified = [],
            Metadata = n.Metadata,
            EventType = n.EventType
        });

        return Collection.InsertManyAsync(entities);
    }

    /// <inheritdoc />
    public Task MarkAsRead(Guid notificationId, Guid userId) =>
        Collection.UpdateOneAsync(
            Filter.Eq(n => n.NotificationId, notificationId) &
            UserToBeNotified(userId),
            Update.Push(n => n.UsersNotified, userId));

    /// <inheritdoc />
    public Task MarkAsRead(Guid userId) =>
        Collection.UpdateManyAsync(
            UserToBeNotified(userId),
            Update.Push(n => n.UsersNotified, userId));

    // ═══ PRIVATE ═══

    /// <summary>
    /// Filter out only notifications that given user is interested with
    /// </summary>
    private static FilterDefinition<Notification> UserInterested(Guid userId) =>
        Filter.AnyEq(n => n.UsersInterested, userId);

    /// <summary>
    /// Filter out only notifications that given user is interested with but haven't yet read
    /// </summary>
    private static FilterDefinition<Notification> UserToBeNotified(Guid userId) =>
        UserInterested(userId) &
        !Filter.AnyEq(n => n.UsersNotified, userId);
}
