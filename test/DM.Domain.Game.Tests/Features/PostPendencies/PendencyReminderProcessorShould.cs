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
using AwesomeAssertions;
using NSubstitute;
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

    private readonly IPostPendencyRepository _repository;
    private readonly IEventProducer _producer;
    private readonly PendencyReminderProcessor _processor;

    private readonly List<string> _calls = [];

    public PendencyReminderProcessorShould()
    {
        _repository = Mock<IPostPendencyRepository>();
        _producer = Mock<IEventProducer>();

        var clock = Mock<IDateTimeProvider>();
        clock.Now.Returns(Now);

        _repository
            .ClaimPendingReminders(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()).Returns(new[] { Guid.NewGuid(), Guid.NewGuid() }).AndDoes(_ => _calls.Add("claim"));

        _producer
            .SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => _calls.Add("publish"));

        _processor = new PendencyReminderProcessor(_repository, _producer, clock);
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
        await _producer.Received(2).SendAsync(EventType.RoomPendencyReminder, Arg.Any<Guid>());
    }

    [Fact]
    public async Task WaitTheFirstReminderWindowBeforeProddingAnybody()
    {
        await _processor.SendDueRemindersAsync();

        await _repository.Received(1).ClaimPendingReminders(
            Now - PendencyPolicy.FirstReminderAfter,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveTheReminderIntervalBetweenTwoRemindersOfTheSamePendency()
    {
        await _processor.SendDueRemindersAsync();

        await _repository.Received(1).ClaimPendingReminders(
            Arg.Any<DateTimeOffset>(),
            Now - PendencyPolicy.ReminderInterval,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
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

        await _repository.Received(1).ClaimPendingReminders(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceNothingWhenNothingIsDue()
    {
        _repository
            .ClaimPendingReminders(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        (await _processor.SendDueRemindersAsync()).Should().Be(0);
        await _producer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }
}
