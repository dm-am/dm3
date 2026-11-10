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

// The order matters: a reminder sent before its stamp is saved goes out again on the next pass.
public class PendencyReminderProcessorShould : UnitTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IPostPendencyRepository repository;
    private readonly IEventProducer producer;
    private readonly PendencyReminderProcessor processor;

    private readonly List<string> calls = [];

    public PendencyReminderProcessorShould()
    {
        repository = Mock<IPostPendencyRepository>();
        producer = Mock<IEventProducer>();

        var clock = Mock<IDateTimeProvider>();
        clock.Now.Returns(Now);

        repository
            .ClaimPendingReminders(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(new[] { Guid.NewGuid(), Guid.NewGuid() })
            .AndDoes(_ => calls.Add("claim"));

        producer
            .SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => calls.Add("publish"));

        processor = new PendencyReminderProcessor(repository, producer, clock);
    }

    // The stamp is what stops the same reminder going out on every pass, so it has to be durable
    // before the first message leaves.
    [Fact]
    public async Task ClaimEveryPendencyBeforeAnnouncingAnyOfThem()
    {
        await processor.SendDueRemindersAsync();

        calls.Should().Equal("claim", "publish", "publish");
    }

    [Fact]
    public async Task AnnounceOneReminderPerClaimedPendency()
    {
        var sent = await processor.SendDueRemindersAsync();

        sent.Should().Be(2);
        await producer.Received(2).SendAsync(EventType.RoomPendencyReminder, Arg.Any<Guid>());
    }

    [Fact]
    public async Task WaitTheFirstWindowAndTheIntervalBeforeProddingAnybody()
    {
        await processor.SendDueRemindersAsync();

        await repository.Received(1).ClaimPendingReminders(
            Now - PendencyPolicy.FirstReminderAfter,
            Now - PendencyPolicy.ReminderInterval,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    // The moment the cutoffs are measured back from and the moment written on the claimed rows are
    // the same instant, which is what a test moving the clock has to be able to move.
    [Fact]
    public async Task StampTheClaimWithTheMomentItMeasuredFrom()
    {
        await processor.SendDueRemindersAsync();

        await repository.Received(1).ClaimPendingReminders(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceNothingWhenNothingIsDue()
    {
        repository
            .ClaimPendingReminders(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());

        (await processor.SendDueRemindersAsync()).Should().Be(0);
        await producer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }
}
