using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Notifications.BusinessProcesses.Reading;
using DM.Services.Notifications.Dto;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Services.Notifications.Tests.BusinessProcesses.Reading;

public class NotificationsReadingServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> identityProvider;
    private readonly Mock<IIdentity> identity;
    private readonly Mock<INotificationsReadingRepository> repository;
    private readonly NotificationsReadingService service;

    public NotificationsReadingServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        identity = Mock<IIdentity>();
        repository = Mock<INotificationsReadingRepository>();

        identityProvider.Setup(p => p.Current).Returns(identity.Object);

        service = new NotificationsReadingService(
            identityProvider.Object,
            repository.Object);
    }

    [Fact]
    public async Task CountUnreadNotificationsForCurrentUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var expectedCount = 5L;
        repository.Setup(r => r.CountUnread(userId))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await service.CountUnread();

        // Assert
        result.Should().Be(expectedCount);
        repository.Verify(r => r.CountUnread(userId), Times.Once);
    }

    [Fact]
    public async Task ReturnZeroWhenNoUnreadNotifications()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        repository.Setup(r => r.CountUnread(userId))
            .ReturnsAsync(0L);

        // Act
        var result = await service.CountUnread();

        // Assert
        result.Should().Be(0L);
    }

    [Fact]
    public async Task GetNotificationsWithDefaultPagingSize()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var query = new PagingQuery { Number = 1, Size = null };
        var totalCount = 25L;

        repository.Setup(r => r.Count(userId))
            .ReturnsAsync(totalCount);

        var expectedNotifications = new List<UserNotification>
        {
            new() { NotificationId = Guid.NewGuid(), EventType = EventType.NewForumTopic },
            new() { NotificationId = Guid.NewGuid(), EventType = EventType.NewMessage }
        };

        repository.Setup(r => r.GetNotifications(userId, It.IsAny<PagingData>()))
            .ReturnsAsync(expectedNotifications);

        // Act
        var result = await service.Get(query);

        // Assert
        result.Should().BeEquivalentTo(expectedNotifications);
        repository.Verify(r => r.Count(userId), Times.Once);
        repository.Verify(r => r.GetNotifications(userId, It.Is<PagingData>(
            p => p.Take == 10)), Times.Once);
    }

    [Fact]
    public async Task GetNotificationsWithCorrectPagingData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var query = new PagingQuery { Number = 2, Size = 15 };
        var totalCount = 50L;

        repository.Setup(r => r.Count(userId))
            .ReturnsAsync(totalCount);

        var expectedNotifications = new List<UserNotification>();
        repository.Setup(r => r.GetNotifications(userId, It.IsAny<PagingData>()))
            .ReturnsAsync(expectedNotifications);

        // Act
        await service.Get(query);

        // Assert
        repository.Verify(r => r.Count(userId), Times.Once);
        repository.Verify(r => r.GetNotifications(userId, It.IsAny<PagingData>()), Times.Once);
    }

    [Fact]
    public async Task GetNotificationsForCurrentUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var query = new PagingQuery { Number = 1 };
        repository.Setup(r => r.Count(userId)).ReturnsAsync(10L);
        repository.Setup(r => r.GetNotifications(userId, It.IsAny<PagingData>()))
            .ReturnsAsync(new List<UserNotification>());

        // Act
        await service.Get(query);

        // Assert
        identityProvider.Verify(p => p.Current, Times.AtLeastOnce);
        identity.Verify(i => i.User, Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReturnEmptyListWhenNoNotificationsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var query = new PagingQuery { Number = 1 };
        repository.Setup(r => r.Count(userId)).ReturnsAsync(0L);
        repository.Setup(r => r.GetNotifications(userId, It.IsAny<PagingData>()))
            .ReturnsAsync(new List<UserNotification>());

        // Act
        var result = await service.Get(query);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNotificationsWithVariousEventTypes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var query = new PagingQuery { Number = 1 };
        repository.Setup(r => r.Count(userId)).ReturnsAsync(3L);

        var expectedNotifications = new List<UserNotification>
        {
            new()
            {
                NotificationId = Guid.NewGuid(),
                EventType = EventType.NewForumTopic,
                CreatedUtc = DateTimeOffset.UtcNow,
                Metadata = new { TopicId = Guid.NewGuid() }
            },
            new()
            {
                NotificationId = Guid.NewGuid(),
                EventType = EventType.NewMessage,
                CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                Metadata = new { MessageId = Guid.NewGuid() }
            },
            new()
            {
                NotificationId = Guid.NewGuid(),
                EventType = EventType.NewCharacter,
                CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                Metadata = new { CharacterId = Guid.NewGuid() }
            }
        };

        repository.Setup(r => r.GetNotifications(userId, It.IsAny<PagingData>()))
            .ReturnsAsync(expectedNotifications);

        // Act
        var result = await service.Get(query);

        // Assert
        var notifications = result.ToList();
        notifications.Should().HaveCount(3);
        notifications.Should().Contain(n => n.EventType == EventType.NewForumTopic);
        notifications.Should().Contain(n => n.EventType == EventType.NewMessage);
        notifications.Should().Contain(n => n.EventType == EventType.NewCharacter);
    }

    [Fact]
    public async Task CalculateTotalCountBeforeGettingNotifications()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var query = new PagingQuery { Number = 1 };
        var callSequence = new List<string>();

        repository.Setup(r => r.Count(userId))
            .Callback(() => callSequence.Add("Count"))
            .ReturnsAsync(10L);

        repository.Setup(r => r.GetNotifications(userId, It.IsAny<PagingData>()))
            .Callback(() => callSequence.Add("GetNotifications"))
            .ReturnsAsync(new List<UserNotification>());

        // Act
        await service.Get(query);

        // Assert
        callSequence.Should().Equal("Count", "GetNotifications");
    }
}
