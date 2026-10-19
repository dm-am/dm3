using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.GlobalChatEvents;

public class GlobalChatEventFactoryShould : UnitTestBase
{
    private readonly GlobalChatEventFactory factory;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;

    public GlobalChatEventFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        factory = new GlobalChatEventFactory(
            guidFactory,
            dateTimeProvider);
    }

    [Fact]
    public void CreateGlobalChatEventWithValidProperties()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var startsAt = DateTimeOffset.UtcNow.AddDays(1);
        var duration = TimeSpan.FromHours(2);

        var createEvent = new CreateGlobalChatEvent
        {
            Title = "Test Event",
            Description = "Test Description",
            StartsUtc = startsAt,
            Duration = duration,
            IsOpen = true
        };

        guidFactory.Create().Returns(eventId);
        dateTimeProvider.Now.Returns(now);

        var result = factory.Create(createEvent, userId);

        result.GlobalChatEventId.Should().Be(eventId);
        result.Title.Should().Be(createEvent.Title);
        result.Description.Should().Be(createEvent.Description);
        result.StartsUtc.Should().Be(startsAt);
        result.Duration.Should().Be(duration);
        result.IsOpen.Should().BeTrue();
        result.Status.Should().Be(GlobalChatEventStatus.Scheduled);
        result.CreatedByUserId.Should().Be(userId);
        result.CreatedUtc.Should().Be(now);
    }

    [Fact]
    public void UseDefaultDurationWhenNotSpecified()
    {
        var createEvent = new CreateGlobalChatEvent
        {
            Title = "Test Event",
            StartsUtc = DateTimeOffset.UtcNow.AddDays(1),
            Duration = null,
            IsOpen = true
        };

        guidFactory.Create().Returns(Guid.NewGuid());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = factory.Create(createEvent, Guid.NewGuid());

        result.Duration.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void UseEmptyDescriptionWhenNull()
    {
        var createEvent = new CreateGlobalChatEvent
        {
            Title = "Test Event",
            Description = null,
            StartsUtc = DateTimeOffset.UtcNow.AddDays(1),
            IsOpen = true
        };

        guidFactory.Create().Returns(Guid.NewGuid());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = factory.Create(createEvent, Guid.NewGuid());

        result.Description.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateParticipantWithCorrectProperties(bool isOrganizer)
    {
        var participantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        guidFactory.Create().Returns(participantId);
        dateTimeProvider.Now.Returns(now);

        var result = factory.CreateParticipant(eventId, userId, isOrganizer);

        result.GlobalChatEventParticipantId.Should().Be(participantId);
        result.GlobalChatEventId.Should().Be(eventId);
        result.UserId.Should().Be(userId);
        result.IsOrganizer.Should().Be(isOrganizer);
        result.JoinedUtc.Should().Be(now);
    }
}
