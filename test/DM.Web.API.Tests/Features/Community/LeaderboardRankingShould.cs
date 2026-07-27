using System.Collections.Generic;
using System.Linq;
using DM.Web.API.Features.Community.Statistics;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

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

        CommunityStatsApiService.AssignCompetitionRanks(entries);

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

        CommunityStatsApiService.AssignCompetitionRanks(entries);

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

        CommunityStatsApiService.AssignCompetitionRanks(entries);

        entries.Select(e => e.Name).Should().Equal("Аня", "Боря", "Витя");
        entries.Select(e => e.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void HandleEmptyBoard()
    {
        var entries = new List<LeaderboardEntry>();

        CommunityStatsApiService.AssignCompetitionRanks(entries);

        entries.Should().BeEmpty();
    }
}
