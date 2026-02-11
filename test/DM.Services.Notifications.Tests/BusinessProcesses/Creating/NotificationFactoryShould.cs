using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.Notifications.BusinessProcesses.Creating;
using DM.Services.Notifications.Dto;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Services.Notifications.Tests.BusinessProcesses.Creating;

public class NotificationFactoryShould : UnitTestBase
{
    private readonly Mock<IGuidFactory> guidFactory;
    private readonly NotificationFactory factory;

    public NotificationFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();
        factory = new NotificationFactory(guidFactory.Object);
    }

    [Fact]
    public void CreateNotificationWithGeneratedId()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        guidFactory.Setup(f => f.Create()).Returns(expectedId);

        var createNotification = new CreateNotification
        {
            EventType = EventType.NewForumTopic,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = new { TopicId = Guid.NewGuid() }
        };

        var createDate = DateTimeOffset.UtcNow;

        // Act
        var result = factory.Create(createNotification, createDate);

        // Assert
        result.NotificationId.Should().Be(expectedId);
        guidFactory.Verify(f => f.Create(), Times.Once);
    }

    [Fact]
    public void CreateNotificationWithCorrectCreateDate()
    {
        // Arrange
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var createNotification = new CreateNotification
        {
            EventType = EventType.NewMessage,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = new { MessageId = Guid.NewGuid() }
        };

        var createDate = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var result = factory.Create(createNotification, createDate);

        // Assert
        result.CreatedUtc.Should().Be(createDate.UtcDateTime);
        result.CreatedUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void CreateNotificationWithCorrectEventType()
    {
        // Arrange
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var createNotification = new CreateNotification
        {
            EventType = EventType.NewCharacter,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = new { CharacterId = Guid.NewGuid() }
        };

        // Act
        var result = factory.Create(createNotification, DateTimeOffset.UtcNow);

        // Assert
        result.EventType.Should().Be(EventType.NewCharacter);
    }

    [Fact]
    public void CreateNotificationWithEmptyUsersNotifiedList()
    {
        // Arrange
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var createNotification = new CreateNotification
        {
            EventType = EventType.NewForumComment,
            UsersInterested = new[] { Guid.NewGuid(), Guid.NewGuid() },
            Metadata = new { CommentId = Guid.NewGuid() }
        };

        // Act
        var result = factory.Create(createNotification, DateTimeOffset.UtcNow);

        // Assert
        result.UsersNotified.Should().NotBeNull();
        result.UsersNotified.Should().BeEmpty();
    }

    [Fact]
    public void CreateNotificationWithCorrectUsersInterested()
    {
        // Arrange
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        var usersInterested = new[] { user1, user2, user3 };

        var createNotification = new CreateNotification
        {
            EventType = EventType.NewGame,
            UsersInterested = usersInterested,
            Metadata = new { GameId = Guid.NewGuid() }
        };

        // Act
        var result = factory.Create(createNotification, DateTimeOffset.UtcNow);

        // Assert
        result.UsersInterested.Should().BeEquivalentTo(usersInterested);
        result.UsersInterested.Should().HaveCount(3);
    }

    [Fact]
    public void CreateNotificationWithCorrectMetadata()
    {
        // Arrange
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var metadata = new { GameId = Guid.NewGuid(), GameTitle = "Test Game" };
        var createNotification = new CreateNotification
        {
            EventType = EventType.NewGame,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = metadata
        };

        // Act
        var result = factory.Create(createNotification, DateTimeOffset.UtcNow);

        // Assert
        result.Metadata.Should().Be(metadata);
    }
}
