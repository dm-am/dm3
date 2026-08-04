using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Game.Features.Popularity;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Popularity;

/// <summary>
/// What the site means by a popular game.
/// </summary>
/// <remarks>
/// Two statements, and until this existed neither could be made without starting
/// the HTTP host: the score is the number of active people who play plus the
/// number who read, and "active" is the product's window rather than this
/// calculation's own. The definition lived twice, written out query for query in
/// a background job and in the seeder, and the two windows had already drifted
/// apart — the fixture said thirty days in figures while the site read the
/// constant.
/// </remarks>
public class GamePopularityProcessorShould : UnitTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Played = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Read = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Ignored = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IGamePopularityRepository> _repository;
    private readonly GamePopularityProcessor _processor;

    private IReadOnlyDictionary<Guid, int>? _written;

    public GamePopularityProcessorShould()
    {
        _repository = Mock<IGamePopularityRepository>();
        _repository
            .Setup(r => r.GetScorableGameIds(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Played, Read, Ignored });
        _repository
            .Setup(r => r.CountActivePlayers(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [Played] = 4, [Read] = 1 });
        _repository
            .Setup(r => r.CountActiveReaders(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [Played] = 3, [Read] = 9 });
        _repository
            .Setup(r => r.ApplyScores(
                It.IsAny<IReadOnlyDictionary<Guid, int>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyDictionary<Guid, int>, DateTimeOffset, CancellationToken>(
                (scores, _, _) => _written = scores)
            .ReturnsAsync(2);

        _processor = new GamePopularityProcessor(_repository.Object);
    }

    [Fact]
    public async Task AddPlayersAndReadersTogether()
    {
        await _processor.UpdateScoresAsync(Now);

        _written.Should().NotBeNull();
        _written![Played].Should().Be(7, "four people play it and three read it");
        _written[Read].Should().Be(10, "one person plays it and nine read it");
    }

    [Fact]
    public async Task ScoreZeroForAGameNobodyActiveTouches()
    {
        await _processor.UpdateScoresAsync(Now);

        _written.Should().ContainKey(Ignored,
            "a game that fell to zero has to be written down as zero, or it keeps the " +
            "score it had when it was busy");
        _written![Ignored].Should().Be(0);
    }

    /// <summary>
    /// The window is the product's, and it is the same window on both counts: a
    /// player active thirty-one days ago is no more active than a reader is.
    /// </summary>
    [Fact]
    public async Task CountAsActiveExactlyWhatTheProductCallsActive()
    {
        await _processor.UpdateScoresAsync(Now);

        var expected = Now - ActivityPolicy.ActivePeriod;

        _repository.Verify(r => r.CountActivePlayers(
            It.IsAny<IReadOnlyCollection<Guid>>(), expected, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.CountActiveReaders(
            It.IsAny<IReadOnlyCollection<Guid>>(), expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The moment comes from the caller, because the two callers disagree about
    /// it on purpose: the host means now, the seeder means the instant its
    /// fixture is dated from.
    /// </summary>
    [Fact]
    public async Task MeasureFromTheMomentItWasGiven()
    {
        var seedEpoch = new DateTimeOffset(2020, 6, 15, 0, 0, 0, TimeSpan.Zero);

        await _processor.UpdateScoresAsync(seedEpoch);

        _repository.Verify(r => r.CountActiveReaders(
            It.IsAny<IReadOnlyCollection<Guid>>(),
            seedEpoch - ActivityPolicy.ActivePeriod,
            It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.ApplyScores(
            It.IsAny<IReadOnlyDictionary<Guid, int>>(), seedEpoch, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CountNothingWhenThereIsNothingToScore()
    {
        _repository
            .Setup(r => r.GetScorableGameIds(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var (updated, total) = await _processor.UpdateScoresAsync(Now);

        updated.Should().Be(0);
        total.Should().Be(0);
        _repository.Verify(r => r.ApplyScores(
            It.IsAny<IReadOnlyDictionary<Guid, int>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
