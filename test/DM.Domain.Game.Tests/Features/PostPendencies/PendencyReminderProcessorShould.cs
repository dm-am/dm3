using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Game.Configuration;
using DM.Domain.Game.Features.PostPendencies;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostPendencies;

/// <summary>
/// When a player is prodded about a post the room is waiting for.
/// </summary>
/// <remarks>
/// The two thresholds and the order of the two side effects lived inside a
/// background job of the HTTP host, where nothing but a running host could reach
/// them. The order is the part that cost something: the loop stamped every stale
/// pendency in memory, published a reminder for each and saved afterwards, so a
/// failure past the publish sent letters the database held no trace of and sent
/// them again twelve hours later.
/// </remarks>
public class PendencyReminderProcessorShould : UnitTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IPostPendencyRepository> _repository;
    private readonly Mock<IEventProducer> _producer;
    private readonly PendencyReminderProcessor _processor;

    private readonly List<string> _calls = [];

    public PendencyReminderProcessorShould()
    {
        _repository = Mock<IPostPendencyRepository>();
        _producer = Mock<IEventProducer>();

        var clock = Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(Now);

        _repository
            .Setup(r => r.ClaimPendingReminders(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("claim"))
            .ReturnsAsync(new[] { Guid.NewGuid(), Guid.NewGuid() });

        _producer
            .Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Callback(() => _calls.Add("publish"))
            .Returns(Task.CompletedTask);

        _processor = new PendencyReminderProcessor(_repository.Object, _producer.Object, clock.Object);
    }

    /// <summary>
    /// The stamp is what stops the same reminder going out on every pass, so it
    /// has to be durable before the first message leaves.
    /// </summary>
    [Fact]
    public async Task ClaimEveryPendencyBeforeAnnouncingAnyOfThem()
    {
        await _processor.SendDueRemindersAsync();

        _calls.Should().Equal("claim", "publish", "publish");
    }

    [Fact]
    public async Task AnnounceOneReminderPerClaimedPendency()
    {
        var sent = await _processor.SendDueRemindersAsync();

        sent.Should().Be(2);
        _producer.Verify(p => p.SendAsync(EventType.RoomPendencyReminder, It.IsAny<Guid>()), Times.Exactly(2));
    }

    [Fact]
    public async Task WaitTheFirstReminderWindowBeforeProddingAnybody()
    {
        await _processor.SendDueRemindersAsync();

        _repository.Verify(r => r.ClaimPendingReminders(
            Now - PendencyPolicy.FirstReminderAfter,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveTheReminderIntervalBetweenTwoRemindersOfTheSamePendency()
    {
        await _processor.SendDueRemindersAsync();

        _repository.Verify(r => r.ClaimPendingReminders(
            It.IsAny<DateTimeOffset>(),
            Now - PendencyPolicy.ReminderInterval,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The moment the cutoffs are measured back from and the moment written on
    /// the claimed rows are the same instant, which is what a test moving the
    /// clock has to be able to move.
    /// </summary>
    [Fact]
    public async Task StampTheClaimWithTheMomentItMeasuredFrom()
    {
        await _processor.SendDueRemindersAsync();

        _repository.Verify(r => r.ClaimPendingReminders(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnnounceNothingWhenNothingIsDue()
    {
        _repository
            .Setup(r => r.ClaimPendingReminders(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        (await _processor.SendDueRemindersAsync()).Should().Be(0);
        _producer.Verify(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()), Times.Never);
    }
}
