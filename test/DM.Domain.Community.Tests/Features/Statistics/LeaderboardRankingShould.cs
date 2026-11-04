using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Caching;
using DM.Domain.Community.Features.Statistics;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Statistics;

/// <summary>
/// Covers the leaderboard display ordering: score DESC with a deterministic
/// name tie-break, plain ordinal ranks 1..N (a top list reads as a simple
/// numbered list — ties never share a rank number).
/// </summary>
public class LeaderboardRankingShould
{
    private static LeaderboardEntry Entry(string name, int score) =>
        new() { Name = name, Score = score };

    [Fact]
    public void AssignSequentialRanksWithoutTies()
    {
        var entries = new List<LeaderboardEntry>
        {
            Entry("c", 5), Entry("a", 12), Entry("b", 7),
        };

        CommunityStatsService.AssignOrdinalRanks(entries);

        entries.Select(e => e.Name).Should().Equal("a", "b", "c");
        entries.Select(e => e.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void KeepOrdinalNumbersForEqualScores()
    {
        var entries = new List<LeaderboardEntry>
        {
            Entry("a", 9), Entry("b", 5), Entry("c", 5), Entry("d", 3),
        };

        CommunityStatsService.AssignOrdinalRanks(entries);

        // Plain 1..N, never 1, 2, 2, 4.
        entries.Select(e => e.Rank).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void OrderTiedEntriesByNameForAStableDisplay()
    {
        var entries = new List<LeaderboardEntry>
        {
            Entry("Витя", 5), Entry("Аня", 5), Entry("Боря", 5),
        };

        CommunityStatsService.AssignOrdinalRanks(entries);

        entries.Select(e => e.Name).Should().Equal("Аня", "Боря", "Витя");
        entries.Select(e => e.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void HandleEmptyBoard()
    {
        var entries = new List<LeaderboardEntry>();

        CommunityStatsService.AssignOrdinalRanks(entries);

        entries.Should().BeEmpty();
    }

    /// <summary>
    /// Every board the page shows comes back sorted and numbered, under the period asked.
    /// </summary>
    [Fact]
    public async Task RankEveryBoardOfThePeriodAsked()
    {
        static List<LeaderboardEntry> Board() => [Entry("b", 3), Entry("a", 8)];
        var boards = new Leaderboards
        {
            TopPlayersByRating = Board(),
            TopPlayersByPosts = Board(),
            TopGamesByRating = Board(),
            TopGamesByPosts = Board(),
            TopPlayersByVolume = Board(),
            TopBlogsByRating = Board(),
            TopBlogsByPosts = Board(),
            TopBlogAuthorsByVolume = Board(),
        };
        var repository = Substitute.For<ICommunityStatsRepository>();
        repository.GetLeaderboards(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(boards);
        var cache = Substitute.For<ICache>();
        cache.GetOrCreateAsync(Arg.Any<object>(), Arg.Any<Func<Task<Leaderboards>>>(), Arg.Any<TimeSpan>())
            .Returns(call => call.ArgAt<Func<Task<Leaderboards>>>(1)());
        var service = new CommunityStatsService(repository, Substitute.For<IDateTimeProvider>(), cache);

        var result = await service.GetLeaderboardsAsync(2026, 3);

        result.Period.Year.Should().Be(2026);
        result.Period.Month.Should().Be(3);
        var all = new[]
        {
            result.TopPlayersByRating, result.TopPlayersByPosts, result.TopGamesByRating,
            result.TopGamesByPosts, result.TopPlayersByVolume, result.TopBlogsByRating,
            result.TopBlogsByPosts, result.TopBlogAuthorsByVolume,
        };
        all.Should().AllSatisfy(board =>
        {
            board.Select(e => e.Name).Should().Equal("a", "b");
            board.Select(e => e.Rank).Should().Equal(1, 2);
        });
    }
}
