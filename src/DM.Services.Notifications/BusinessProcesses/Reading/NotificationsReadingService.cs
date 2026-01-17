using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Notifications.Dto;

namespace DM.Services.Notifications.BusinessProcesses.Reading;

/// <inheritdoc />
internal class NotificationsReadingService : INotificationsReadingService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly INotificationsReadingRepository _repository;

    /// <inheritdoc />
    public NotificationsReadingService(
        IIdentityProvider identityProvider,
        INotificationsReadingRepository repository)
    {
        _identityProvider = identityProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    public Task<long> CountUnread() => _repository.CountUnread(_identityProvider.Current.User.UserId);

    /// <inheritdoc />
    public async Task<IEnumerable<UserNotification>> Get(PagingQuery query)
    {
        var userId = _identityProvider.Current.User.UserId;
        var totalCount = await _repository.Count(userId);
        var pagingData = new PagingData(query, 10, (int) totalCount);
        return await _repository.GetNotifications(userId, pagingData);
    }
}