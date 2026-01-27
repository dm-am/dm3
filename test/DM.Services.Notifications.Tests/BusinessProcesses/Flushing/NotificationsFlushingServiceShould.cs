using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Notifications.BusinessProcesses.Flushing;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Services.Notifications.Tests.BusinessProcesses.Flushing;

public class NotificationsFlushingServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> identityProvider;
    private readonly Mock<IIdentity> identity;
    private readonly Mock<INotificationsFlushingRepository> repository;
    private readonly NotificationsFlushingService service;

    public NotificationsFlushingServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        identity = Mock<IIdentity>();
        repository = Mock<INotificationsFlushingRepository>();

        identityProvider.Setup(p => p.Current).Returns(identity.Object);

        service = new NotificationsFlushingService(
            identityProvider.Object,
            repository.Object);
    }

    [Fact]
    public async Task MarkSingleNotificationAsReadForCurrentUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        repository.Setup(r => r.MarkAsRead(notificationId, userId))
            .Returns(Task.CompletedTask);

        // Act
        await service.MarkAsRead(notificationId);

        // Assert
        repository.Verify(r => r.MarkAsRead(notificationId, userId), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadUsesCorrectUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        repository.Setup(r => r.MarkAsRead(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.MarkAsRead(notificationId);

        // Assert
        identityProvider.Verify(p => p.Current, Times.Once);
        identity.Verify(i => i.User, Times.Once);
        repository.Verify(r => r.MarkAsRead(notificationId, userId), Times.Once);
    }

    [Fact]
    public async Task MarkAllNotificationsAsReadForCurrentUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        repository.Setup(r => r.MarkAllAsRead(userId))
            .Returns(Task.CompletedTask);

        // Act
        await service.MarkAllAsRead();

        // Assert
        repository.Verify(r => r.MarkAllAsRead(userId), Times.Once);
    }

    [Fact]
    public async Task MarkAllAsReadUsesCorrectUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        repository.Setup(r => r.MarkAllAsRead(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.MarkAllAsRead();

        // Assert
        identityProvider.Verify(p => p.Current, Times.Once);
        identity.Verify(i => i.User, Times.Once);
        repository.Verify(r => r.MarkAllAsRead(userId), Times.Once);
    }

    [Fact]
    public async Task CompleteSuccessfullyWhenMarkingAsRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        repository.Setup(r => r.MarkAsRead(notificationId, userId))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = async () => await service.MarkAsRead(notificationId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CompleteSuccessfullyWhenMarkingAllAsRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        repository.Setup(r => r.MarkAllAsRead(userId))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = async () => await service.MarkAllAsRead();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PropagateExceptionFromRepositoryWhenMarkingAsRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var expectedException = new InvalidOperationException("Database error");
        repository.Setup(r => r.MarkAsRead(notificationId, userId))
            .ThrowsAsync(expectedException);

        // Act
        Func<Task> act = async () => await service.MarkAsRead(notificationId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database error");
    }

    [Fact]
    public async Task PropagateExceptionFromRepositoryWhenMarkingAllAsRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var expectedException = new InvalidOperationException("Database error");
        repository.Setup(r => r.MarkAllAsRead(userId))
            .ThrowsAsync(expectedException);

        // Act
        Func<Task> act = async () => await service.MarkAllAsRead();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database error");
    }

    [Fact]
    public async Task MarkMultipleNotificationsAsReadSequentially()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authenticatedUser = new AuthenticatedUser { UserId = userId };
        identity.Setup(i => i.User).Returns(authenticatedUser);

        var notificationId1 = Guid.NewGuid();
        var notificationId2 = Guid.NewGuid();
        var notificationId3 = Guid.NewGuid();

        repository.Setup(r => r.MarkAsRead(It.IsAny<Guid>(), userId))
            .Returns(Task.CompletedTask);

        // Act
        await service.MarkAsRead(notificationId1);
        await service.MarkAsRead(notificationId2);
        await service.MarkAsRead(notificationId3);

        // Assert
        repository.Verify(r => r.MarkAsRead(notificationId1, userId), Times.Once);
        repository.Verify(r => r.MarkAsRead(notificationId2, userId), Times.Once);
        repository.Verify(r => r.MarkAsRead(notificationId3, userId), Times.Once);
        repository.Verify(r => r.MarkAsRead(It.IsAny<Guid>(), userId), Times.Exactly(3));
    }
}
