using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;

namespace DM.Domain.Personal.Features.Notifications;

/// <inheritdoc />
internal class NotificationService : INotificationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationFactory _factory;
    private readonly INotificationRepository _repository;

    /// <inheritdoc />
    public NotificationService(
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        INotificationFactory factory,
        INotificationRepository repository)
    {
        _identityProvider = identityProvider;
        _dateTimeProvider = dateTimeProvider;
        _factory = factory;
        _repository = repository;
    }

    #region Reading

    /// <inheritdoc />
    public Task<long> CountUnreadAsync() =>
        _repository.CountUnread(_identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public async Task<IEnumerable<UserNotification>> GetAsync(PagingQuery query)
    {
        var userId = _identityProvider.Current.User.UserId;
        var totalCount = await _repository.Count(userId);
        var pagingData = new PagingData(query, 10, (int)totalCount);
        return await _repository.GetNotifications(userId, pagingData);
    }

    #endregion

    #region Creating

    /// <inheritdoc />
    public async Task<IEnumerable<CreateNotificationEntity>> CreateAsync(
        IEnumerable<CreateNotification> createNotifications)
    {
        var createDate = _dateTimeProvider.Now;
        var notifications = createNotifications
            .Select(n => _factory.Create(n, createDate))
            .ToArray();

        await _repository.Create(notifications);

        return notifications;
    }

    #endregion

    #region Flushing

    /// <inheritdoc />
    public Task MarkAsReadAsync(Guid notificationId) =>
        _repository.MarkAsRead(notificationId, _identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public Task MarkAllAsReadAsync() =>
        _repository.MarkAsRead(_identityProvider.Current.User.UserId);

    #endregion
}
