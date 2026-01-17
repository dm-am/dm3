using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Notifications;
using DM.Services.Notifications.Dto;

namespace DM.Services.Notifications.BusinessProcesses.Creating;

/// <inheritdoc />
internal class NotificationCreatingService : INotificationCreatingService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationFactory _factory;
    private readonly INotificationCreatingRepository _repository;

    /// <inheritdoc />
    public NotificationCreatingService(
        IDateTimeProvider dateTimeProvider,
        INotificationFactory factory,
        INotificationCreatingRepository repository)
    {
        _dateTimeProvider = dateTimeProvider;
        _factory = factory;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Notification>> Create(IEnumerable<CreateNotification> createNotifications)
    {
        var createDate = _dateTimeProvider.Now;
        var notifications = createNotifications
            .Select(n => _factory.Create(n, createDate))
            .ToArray();

        await _repository.Create(notifications);

        return notifications;
    }
}