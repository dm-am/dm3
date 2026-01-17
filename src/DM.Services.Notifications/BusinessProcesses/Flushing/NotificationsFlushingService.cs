using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;

namespace DM.Services.Notifications.BusinessProcesses.Flushing;

/// <inheritdoc />
internal class NotificationsFlushingService : INotificationsFlushingService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly INotificationsFlushingRepository _repository;

    /// <inheritdoc />
    public NotificationsFlushingService(
        IIdentityProvider identityProvider,
        INotificationsFlushingRepository repository)
    {
        _identityProvider = identityProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    public Task MarkAsRead(Guid notificationId) =>
        _repository.MarkAsRead(notificationId, _identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public Task MarkAllAsRead() =>
        _repository.MarkAllAsRead(_identityProvider.Current.User.UserId);
}