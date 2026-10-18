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

/// <summary>
/// The digest of a closed period is generated once and never again: the marker
/// row is the whole memory of it. These tests are about what happens to that
/// marker when the topic it was taken for does not get written.
/// </summary>
public class PeriodDigestProcessorShould : UnitTestBase
{
    private readonly IPeriodDigestRepository _digestRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IUnreadCountersRepository _unreadCounters;
    private readonly IEventProducer _eventProducer;
    private readonly IGuidFactory _guidFactory;
    private readonly PeriodDigestProcessor _processor;

    private readonly List<(int Year, int? Month, Guid TopicId)> _claims = [];
    private readonly List<Guid> _releases = [];

    public PeriodDigestProcessorShould()
    {
        _digestRepository = Mock<IPeriodDigestRepository>();
        _topicRepository = Mock<ITopicRepository>();
        _unreadCounters = Mock<IUnreadCountersRepository>();
        _eventProducer = Mock<IEventProducer>();
        _guidFactory = Mock<IGuidFactory>();
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        // A fresh identifier per call, the way the real factory answers: the marker
        // and the topic are named by it, and a constant would hide a release that
        // took back the wrong claim.
        _guidFactory.Create().Returns(_ => Guid.NewGuid());

        // Neither period has been generated, so both are claimed by this pass.
        _digestRepository.DigestExists(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _digestRepository.TryClaimPeriod(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int?>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _claims.Add((ci.ArgAt<int>(1), ci.ArgAt<int?>(2), ci.ArgAt<Guid>(0)));
                return true;
            });
        _digestRepository.ReleasePeriod(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _releases.Add(ci.ArgAt<Guid>(0));
                return Task.FromResult(1);
            });

        _processor = new PeriodDigestProcessor(
            _digestRepository,
            _topicRepository,
            _unreadCounters,
            _eventProducer,
            dateTimeProvider,
            _guidFactory,
            Mock<ILogger<PeriodDigestProcessor>>());
    }

    [Fact]
    public async Task GiveThePeriodBackWhenTheTopicCouldNotBeWritten()
    {
        _topicRepository.Create(
                Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("deadlock"));

        var pass = async () => await _processor.EnsureClosedPeriodsAsync();

        await pass.Should().ThrowAsync<InvalidOperationException>(
            "the pass has to fail out loud rather than report a digest it did not create");
        // The claim is a committed row taken before the topic exists. Left behind,
        // it answers every later pass with "already generated" for a period that
        // has no topic, and a monthly or yearly digest is never generated twice -
        // so that issue would be lost for good.
        _releases.Should().Equal(_claims[0].TopicId);
        _eventProducer.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task KeepThePeriodWhenTheTopicWasWritten()
    {
        _topicRepository.Create(
                Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new Topic { Id = ci.Arg<CreateTopicEntity>().TopicId });

        var created = await _processor.EnsureClosedPeriodsAsync();

        // Past a written topic the marker is telling the truth, and giving it back
        // would let the next pass post the same digest a second time.
        created.Should().HaveCount(2, "the last closed month and the last closed year");
        _releases.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateNothingForAPeriodAnotherInstanceClaimed()
    {
        _digestRepository.TryClaimPeriod(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int?>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var created = await _processor.EnsureClosedPeriodsAsync();

        // The loser of the race wrote nothing, so it has nothing to take back:
        // releasing here would take away the winner's claim.
        created.Should().BeEmpty();
        _releases.Should().BeEmpty();
        await _topicRepository.DidNotReceive().Create(
            Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TakeBackTheUnreadMarkersOfATopicThatWasNotWritten()
    {
        _topicRepository.Create(
                Arg.Any<CreateTopicEntity>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("deadlock"));

        var pass = async () => await _processor.EnsureClosedPeriodsAsync();
        await pass.Should().ThrowAsync<InvalidOperationException>();

        // The markers are written ahead of the row and are only kept by a commit
        // the failed creation never reaches.
        await _unreadCounters.Received(1).DeleteAsync(_claims[0].TopicId, UnreadEntryType.Message);
    }
}
