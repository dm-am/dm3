using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Personal.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

public class NotificationServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<INotificationFactory> _factory;
    private readonly Mock<INotificationRepository> _repository;
    private readonly NotificationService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public NotificationServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _factory = Mock<INotificationFactory>();
        _repository = Mock<INotificationRepository>();

        var settings = new UserSettings
        {
            Paging = new PagingSettings { EntitiesPerPage = 10 }
        };
        var identity = Identity.Authenticated(_currentUserId, "CurrentUser", UserRole.RegularUser, settings);
        _identityProvider.Setup(p => p.Current).Returns(identity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);

        _service = new NotificationService(
            _identityProvider.Object,
            _dateTimeProvider.Object,
            _factory.Object,
            _repository.Object);
    }

    [Fact]
    public async Task CountUnreadNotificationsForCurrentUser()
    {
        _repository.Setup(r => r.CountUnread(_currentUserId))
            .ReturnsAsync(5);

        var result = await _service.CountUnreadAsync();

        result.Should().Be(5);
    }

    [Fact]
    public async Task GetNotificationsWithPaging()
    {
        _repository.Setup(r => r.Count(_currentUserId))
            .ReturnsAsync(25);
        _repository.Setup(r => r.GetNotifications(_currentUserId, It.IsAny<PagingData>()))
            .ReturnsAsync([]);

        var query = new PagingQuery { Skip = 0, Take = 10 };
        var result = await _service.GetAsync(query);

        result.Should().NotBeNull();
        _repository.Verify(r => r.GetNotifications(
            _currentUserId,
            It.Is<PagingData>(p => p.Skip == 0 && p.Take == 10)), Times.Once);
    }

    [Fact]
    public async Task CreateNotificationsWithCorrectDate()
    {
        var createNotifications = new[]
        {
            new CreateNotification
            {
                UsersInterested = new[] { Guid.NewGuid() },
                EventType = EventType.NewMessage,
                Metadata = "Notification 1"
            },
            new CreateNotification
            {
                UsersInterested = new[] { Guid.NewGuid() },
                EventType = EventType.NewMessage,
                Metadata = "Notification 2"
            }
        };

        _factory.Setup(f => f.Create(It.IsAny<CreateNotification>(), _now))
            .Returns<CreateNotification, DateTimeOffset>((n, d) =>
                new CreateNotificationEntity
                {
                    UsersInterested = n.UsersInterested,
                    EventType = n.EventType,
                    Metadata = n.Metadata
                });

        _repository.Setup(r => r.Create(It.IsAny<CreateNotificationEntity[]>()))
            .Returns(Task.CompletedTask);

        await _service.CreateAsync(createNotifications);

        _factory.Verify(f => f.Create(It.IsAny<CreateNotification>(), _now), Times.Exactly(2));
        _repository.Verify(r => r.Create(It.IsAny<CreateNotificationEntity[]>()), Times.Once);
    }

    [Fact]
    public async Task MarkSingleNotificationAsRead()
    {
        var notificationId = Guid.NewGuid();
        _repository.Setup(r => r.MarkAsRead(notificationId, _currentUserId))
            .Returns(Task.CompletedTask);

        await _service.MarkAsReadAsync(notificationId);

        _repository.Verify(r => r.MarkAsRead(notificationId, _currentUserId), Times.Once);
    }

    [Fact]
    public async Task MarkAllNotificationsAsRead()
    {
        _repository.Setup(r => r.MarkAsRead(_currentUserId))
            .Returns(Task.CompletedTask);

        await _service.MarkAllAsReadAsync();

        _repository.Verify(r => r.MarkAsRead(_currentUserId), Times.Once);
    }
}
