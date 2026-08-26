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
    private readonly GlobalChatEventFactory _factory;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GlobalChatEventFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _factory = new GlobalChatEventFactory(
            _guidFactory,
            _dateTimeProvider);
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

        _guidFactory.Create().Returns(eventId);
        _dateTimeProvider.Now.Returns(now);

        var result = _factory.Create(createEvent, userId);

        result.Should().NotBeNull();
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

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(createEvent, Guid.NewGuid());

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

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(createEvent, Guid.NewGuid());

        result.Description.Should().BeEmpty();
    }

    [Fact]
    public void CreateParticipantWithCorrectProperties()
    {
        var participantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _guidFactory.Create().Returns(participantId);
        _dateTimeProvider.Now.Returns(now);

        var result = _factory.CreateParticipant(eventId, userId, isOrganizer: true);

        result.Should().NotBeNull();
        result.GlobalChatEventParticipantId.Should().Be(participantId);
        result.GlobalChatEventId.Should().Be(eventId);
        result.UserId.Should().Be(userId);
        result.IsOrganizer.Should().BeTrue();
        result.JoinedUtc.Should().Be(now);
    }

    [Fact]
    public void CreateParticipantAsNonOrganizer()
    {
        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.CreateParticipant(Guid.NewGuid(), Guid.NewGuid(), isOrganizer: false);

        result.IsOrganizer.Should().BeFalse();
    }
}
