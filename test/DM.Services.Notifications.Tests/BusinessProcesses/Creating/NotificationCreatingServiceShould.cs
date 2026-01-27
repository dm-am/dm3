using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Notifications;
using DM.Services.Notifications.BusinessProcesses.Creating;
using DM.Services.Notifications.Dto;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Services.Notifications.Tests.BusinessProcesses.Creating;

public class NotificationCreatingServiceShould : UnitTestBase
{
    private readonly Mock<IDateTimeProvider> dateTimeProvider;
    private readonly Mock<INotificationFactory> factory;
    private readonly Mock<INotificationCreatingRepository> repository;
    private readonly NotificationCreatingService service;

    public NotificationCreatingServiceShould()
    {
        dateTimeProvider = Mock<IDateTimeProvider>();
        factory = Mock<INotificationFactory>();
        repository = Mock<INotificationCreatingRepository>();

        service = new NotificationCreatingService(
            dateTimeProvider.Object,
            factory.Object,
            repository.Object);
    }

    [Fact]
    public async Task CreateSingleNotificationSuccessfully()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Setup(p => p.Now).Returns(now);

        var createNotification = new CreateNotification
        {
            EventType = EventType.NewForumTopic,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = new { TopicId = Guid.NewGuid() }
        };

        var expectedNotification = new Notification
        {
            NotificationId = Guid.NewGuid(),
            EventType = EventType.NewForumTopic,
            CreateDate = now.UtcDateTime,
            UsersInterested = createNotification.UsersInterested,
            UsersNotified = new List<Guid>(),
            Metadata = createNotification.Metadata
        };

        factory.Setup(f => f.Create(createNotification, now))
            .Returns(expectedNotification);

        repository.Setup(r => r.Create(It.IsAny<IEnumerable<Notification>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.Create(new[] { createNotification });

        // Assert
        var notifications = result.ToList();
        notifications.Should().HaveCount(1);
        notifications[0].Should().Be(expectedNotification);

        factory.Verify(f => f.Create(createNotification, now), Times.Once);
        repository.Verify(r => r.Create(It.Is<IEnumerable<Notification>>(
            n => n.Count() == 1 && n.First() == expectedNotification)), Times.Once);
    }

    [Fact]
    public async Task CreateMultipleNotificationsSuccessfully()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Setup(p => p.Now).Returns(now);

        var createNotification1 = new CreateNotification
        {
            EventType = EventType.NewForumTopic,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = new { TopicId = Guid.NewGuid() }
        };

        var createNotification2 = new CreateNotification
        {
            EventType = EventType.NewMessage,
            UsersInterested = new[] { Guid.NewGuid(), Guid.NewGuid() },
            Metadata = new { MessageId = Guid.NewGuid() }
        };

        var notification1 = new Notification
        {
            NotificationId = Guid.NewGuid(),
            EventType = EventType.NewForumTopic
        };

        var notification2 = new Notification
        {
            NotificationId = Guid.NewGuid(),
            EventType = EventType.NewMessage
        };

        factory.Setup(f => f.Create(createNotification1, now)).Returns(notification1);
        factory.Setup(f => f.Create(createNotification2, now)).Returns(notification2);

        repository.Setup(r => r.Create(It.IsAny<IEnumerable<Notification>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.Create(new[] { createNotification1, createNotification2 });

        // Assert
        var notifications = result.ToList();
        notifications.Should().HaveCount(2);
        notifications.Should().Contain(notification1);
        notifications.Should().Contain(notification2);

        factory.Verify(f => f.Create(It.IsAny<CreateNotification>(), now), Times.Exactly(2));
        repository.Verify(r => r.Create(It.Is<IEnumerable<Notification>>(
            n => n.Count() == 2)), Times.Once);
    }

    [Fact]
    public async Task UseCurrentDateTimeForAllNotifications()
    {
        // Arrange
        var now = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        dateTimeProvider.Setup(p => p.Now).Returns(now);

        var createNotifications = new[]
        {
            new CreateNotification
            {
                EventType = EventType.NewForumTopic,
                UsersInterested = new[] { Guid.NewGuid() },
                Metadata = new { TopicId = Guid.NewGuid() }
            },
            new CreateNotification
            {
                EventType = EventType.NewMessage,
                UsersInterested = new[] { Guid.NewGuid() },
                Metadata = new { MessageId = Guid.NewGuid() }
            }
        };

        factory.Setup(f => f.Create(It.IsAny<CreateNotification>(), It.IsAny<DateTimeOffset>()))
            .Returns(new Notification { NotificationId = Guid.NewGuid() });

        repository.Setup(r => r.Create(It.IsAny<IEnumerable<Notification>>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.Create(createNotifications);

        // Assert
        dateTimeProvider.Verify(p => p.Now, Times.Once);
        factory.Verify(f => f.Create(It.IsAny<CreateNotification>(), now), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateEmptyCollectionWhenNoNotificationsProvided()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Setup(p => p.Now).Returns(now);

        repository.Setup(r => r.Create(It.IsAny<IEnumerable<Notification>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.Create(Array.Empty<CreateNotification>());

        // Assert
        result.Should().BeEmpty();
        factory.Verify(f => f.Create(It.IsAny<CreateNotification>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
        repository.Verify(r => r.Create(It.Is<IEnumerable<Notification>>(n => !n.Any())),
            Times.Once);
    }

    [Fact]
    public async Task ReturnCreatedNotificationsInCorrectOrder()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Setup(p => p.Now).Returns(now);

        var createNotifications = new[]
        {
            new CreateNotification { EventType = EventType.NewForumTopic, UsersInterested = new[] { Guid.NewGuid() } },
            new CreateNotification { EventType = EventType.NewMessage, UsersInterested = new[] { Guid.NewGuid() } },
            new CreateNotification { EventType = EventType.NewCharacter, UsersInterested = new[] { Guid.NewGuid() } }
        };

        var notifications = new[]
        {
            new Notification { NotificationId = Guid.NewGuid(), EventType = EventType.NewForumTopic },
            new Notification { NotificationId = Guid.NewGuid(), EventType = EventType.NewMessage },
            new Notification { NotificationId = Guid.NewGuid(), EventType = EventType.NewCharacter }
        };

        for (int i = 0; i < createNotifications.Length; i++)
        {
            var index = i;
            factory.Setup(f => f.Create(createNotifications[index], now))
                .Returns(notifications[index]);
        }

        repository.Setup(r => r.Create(It.IsAny<IEnumerable<Notification>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.Create(createNotifications);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList[0].EventType.Should().Be(EventType.NewForumTopic);
        resultList[1].EventType.Should().Be(EventType.NewMessage);
        resultList[2].EventType.Should().Be(EventType.NewCharacter);
    }
}
