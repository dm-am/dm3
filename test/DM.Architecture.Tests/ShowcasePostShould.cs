using System.Text.RegularExpressions;
using DM.Domain.Community.Features.Statistics;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The post the home page shows as the best of the week outscores everything
/// else the seed puts into that week.
/// </summary>
/// <remarks>
/// Leaderboard coverage tops up the same window in the first week of a month, so the showcase keeps a
/// margin. Both sides are read out of the sources.
/// </remarks>
public class ShowcasePostShould
{
    private const string Seeder = "src/DM.Tools.Seeder/Seeding";

    [Fact]
    public void OutscoreTheFillerTheLeaderboardPassWrites()
    {
        var showcase = Offset(
            RepositoryFiles.Read(Seeder, "DataSeeder.Showcase.cs"),
            @"chuckReviewCount = Leaderboards\.BoardSize \+ (\d+)",
            "the showcase post");

        var coverage = Offset(
            RepositoryFiles.Read(Seeder, "DataSeeder.Leaderboards.cs"),
            @"target = Leaderboards\.BoardSize \+ (\d+)",
            "leaderboard coverage");

        showcase.Should().BeGreaterThan(coverage,
            "the widget takes the highest sum in the week, and coverage tops one post at " +
            $"{Leaderboards.BoardSize + coverage} by construction. Level with it, which " +
            "post wins is whatever the query plan returns first");
    }

    /// <summary>The number added to the board size in one source.</summary>
    private static int Offset(string source, string pattern, string what)
    {
        var found = Regex.Match(source, pattern);
        found.Success.Should().BeTrue(
            $"{what} derives its score from Leaderboards.BoardSize; written out as a " +
            "number it stops moving when the board does");
        return int.Parse(found.Groups[1].Value);
    }
}
