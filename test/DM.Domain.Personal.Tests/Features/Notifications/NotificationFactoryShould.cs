using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

public class NotificationFactoryShould : UnitTestBase
{
    private readonly NotificationFactory factory;
    private readonly IGuidFactory guidFactory;

    public NotificationFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();

        factory = new NotificationFactory(guidFactory);
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

        guidFactory.Create().Returns(notificationId);

        var result = factory.Create(createNotification, createDate);

        result.NotificationId.Should().Be(notificationId);
        result.CreatedUtc.Should().Be(createDate);
        result.EventType.Should().Be(EventType.NewTopicComment);
        result.UsersInterested.Should().BeEquivalentTo(usersInterested);
        result.Metadata.Should().Be(metadata);
    }

    /// <summary>
    /// The entity is what the repository stores, and the stored EventId is what
    /// a replayed event finds: a factory that dropped it would silently disarm
    /// the deduplication for every notification it builds.
    /// </summary>
    [Fact]
    public void PreserveEventId()
    {
        var eventId = Guid.NewGuid();
        var createNotification = new CreateNotification
        {
            EventType = EventType.NewTopic,
            EventId = eventId,
            UsersInterested = new[] { Guid.NewGuid() },
            Metadata = "{}"
        };

        guidFactory.Create().Returns(Guid.NewGuid());

        var result = factory.Create(createNotification, DateTimeOffset.UtcNow);

        result.EventId.Should().Be(eventId);
    }
}
