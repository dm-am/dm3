using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

public class NotificationFactoryShould : UnitTestBase
{
    private readonly NotificationFactory _factory;
    private readonly Mock<IGuidFactory> _guidFactory;

    public NotificationFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();

        _factory = new NotificationFactory(_guidFactory.Object);
    }

    [Fact]
    public void CreateNotificationWithValidProperties()
    {
        var notificationId = Guid.NewGuid();
        var createDate = DateTimeOffset.UtcNow;
        var usersInterested = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var metadata = "{ \"key\": \"value\" }";

        var createNotification = new CreateNotification
        {
            EventType = EventType.NewTopicComment,
            UsersInterested = usersInterested,
            Metadata = metadata
        };

        _guidFactory.Setup(f => f.Create()).Returns(notificationId);

        var result = _factory.Create(createNotification, createDate);

        result.Should().NotBeNull();
        result.NotificationId.Should().Be(notificationId);
        result.CreatedUtc.Should().Be(createDate);
        result.EventType.Should().Be(EventType.NewTopicComment);
        result.UsersInterested.Should().BeEquivalentTo(usersInterested);
        result.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void PreserveEventType()
    {
        var createNotification = new CreateNotification
        {
            EventType = EventType.NewPost,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = "{}"
        };

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(createNotification, DateTimeOffset.UtcNow);

        result.EventType.Should().Be(EventType.NewPost);
    }

    [Fact]
    public void PreserveUsersInterestedList()
    {
        var usersInterested = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var createNotification = new CreateNotification
        {
            EventType = EventType.NewMessage,
            UsersInterested = usersInterested,
            Metadata = "{}"
        };

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(createNotification, DateTimeOffset.UtcNow);

        result.UsersInterested.Should().BeEquivalentTo(usersInterested);
    }

    [Fact]
    public void PreserveMetadata()
    {
        var metadata = "{ \"gameId\": \"123\", \"roomId\": \"456\" }";
        var createNotification = new CreateNotification
        {
            EventType = EventType.PlayerInvitationCreated,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = metadata
        };

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(createNotification, DateTimeOffset.UtcNow);

        result.Metadata.Should().Be(metadata);
    }
}
