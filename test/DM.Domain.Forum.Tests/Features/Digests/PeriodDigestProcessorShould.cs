using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Forum.Features.Digests;
using DM.Domain.Forum.Features.Topics;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Digests;

// The digest of a closed period is generated once and never again: the marker row is the whole
// memory of it.
public class PeriodDigestProcessorShould : UnitTestBase
{
    private readonly IPeriodDigestRepository digestRepository;
    private readonly ITopicRepository topicRepository;
    private readonly IUnreadCountersRepository unreadCounters;
    private readonly IEventProducer eventProducer;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly PeriodDigestProcessor processor;

    private readonly List<(int Year, int? Month, Guid TopicId)> claims = [];
    private readonly List<Guid> releases = [];

    public PeriodDigestProcessorShould()
    {
        digestRepository = Mock<IPeriodDigestRepository>();
        topicRepository = Mock<ITopicRepository>();
        unreadCounters = Mock<IUnreadCountersRepository>();
        eventProducer = Mock<IEventProducer>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        // A fresh identifier per call, the way the real factory answers: the marker
        // and the topic are named by it, and a constant would hide a release that
        // took back the wrong claim.
        guidFactory.Create().Returns(_ => Guid.NewGuid());

        // Neither period has been generated, so both are claimed by this pass.
        digestRepository.DigestExists(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        digestRepository.TryClaimPeriod(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int?>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                claims.Add((ci.ArgAt<int>(1), ci.ArgAt<int?>(2), ci.ArgAt<Guid>(0)));
                return true;
            });
        digestRepository.ReleasePeriod(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                releases.Add(ci.ArgAt<Guid>(0));
                return Task.FromResult(1);
            });

        processor = new PeriodDigestProcessor(
            digestRepository,
            topicRepository,
            unreadCounters,
            eventProducer,
            dateTimeProvider,
            guidFactory,
            Mock<ILogger<PeriodDigestProcessor>>());
    }

    [Theory]
    [InlineData(1, "Итоги января 2026 года")]
    [InlineData(2, "Итоги февраля 2026 года")]
    [InlineData(3, "Итоги марта 2026 года")]
    [InlineData(4, "Итоги апреля 2026 года")]
    [InlineData(5, "Итоги мая 2026 года")]
    [InlineData(6, "Итоги июня 2026 года")]
    [InlineData(7, "Итоги июля 2026 года")]
    [InlineData(8, "Итоги августа 2026 года")]
    [InlineData(9, "Итоги сентября 2026 года")]
    [InlineData(10, "Итоги октября 2026 года")]
    [InlineData(11, "Итоги ноября 2026 года")]
    [InlineData(12, "Итоги декабря 2026 года")]
    public async Task NameTheMonthInTheGenitive(int month, string title)
    {
        dateTimeProvider.Now.Returns(new DateTimeOffset(2026, month, 15, 0, 0, 0, TimeSpan.Zero).AddMonths(1));
        topicRepository.Create(
                Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new Topic { Id = ci.Arg<CreateTopicEntity>().TopicId });

        var created = await processor.EnsureClosedPeriodsAsync();

        created.Should().Contain(title);
    }

    [Fact]
    public async Task GiveThePeriodBackWhenTheTopicCouldNotBeWritten()
    {
        topicRepository.Create(
                Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("deadlock"));

        var pass = async () => await processor.EnsureClosedPeriodsAsync();

        await pass.Should().ThrowAsync<InvalidOperationException>(
            "the pass has to fail out loud rather than report a digest it did not create");
        // Left behind, the claim answers every later pass with "already generated".
        releases.Should().Equal(claims[0].TopicId);
        eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task KeepThePeriodWhenTheTopicWasWritten()
    {
        topicRepository.Create(
                Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new Topic { Id = ci.Arg<CreateTopicEntity>().TopicId });

        var created = await processor.EnsureClosedPeriodsAsync();

        // Given back, the marker would let the next pass post the same digest again.
        created.Should().HaveCount(2, "the last closed month and the last closed year");
        releases.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateNothingForAPeriodAnotherInstanceClaimed()
    {
        digestRepository.TryClaimPeriod(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int?>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var created = await processor.EnsureClosedPeriodsAsync();

        // Releasing here would take away the winner's claim.
        created.Should().BeEmpty();
        releases.Should().BeEmpty();
        await topicRepository.DidNotReceive().Create(
            Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TakeBackTheUnreadMarkersOfATopicThatWasNotWritten()
    {
        topicRepository.Create(
                Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("deadlock"));

        var pass = async () => await processor.EnsureClosedPeriodsAsync();
        await pass.Should().ThrowAsync<InvalidOperationException>();

        // The markers are kept only by a commit the failed creation never reaches.
        await unreadCounters.Received(1).DeleteAsync(claims[0].TopicId, UnreadEntryType.Message);
    }
}
