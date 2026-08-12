using System;
using System.IO;
using System.Text.RegularExpressions;
using DM.Domain.Community.Features.Statistics;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The post the home page shows as the best of the week outscores everything
/// else the seed puts into that week.
/// </summary>
/// <remarks>
/// The widget ranks by the sum of review scores over the last seven days, and
/// two parts of the seed write into that window. One is the showcase post,
/// written to be read. The other is leaderboard coverage, which tops up posts
/// until ten distinct games and ten distinct authors carry a positive score,
/// and whose reviews all read "Отличный отыгрыш!".
///
/// Coverage was written to keep its reviews before the week starts, and in the
/// first seven days of a month it cannot: a review has to sit inside its month
/// window for the boards to count it, that window opens on the first, and the
/// rolling week reaches back into the month before. The two demands contradict
/// each other there, so for a week out of every four the home page showed a
/// filler post instead of the one written for it.
///
/// The margin is what settles it, and both sides are read out of the sources
/// rather than restated here: a board that grows moves the coverage top with
/// it, and a number spelled out in either place would go stale in silence.
/// </remarks>
public class ShowcasePostShould
{
    private const string Seeder = "src/DM.Tools.Seeder/Seeding";

    [Fact]
    public void OutscoreTheFillerTheLeaderboardPassWrites()
    {
        var showcase = Offset(
            Read(Seeder, "DataSeeder.Reviews.cs"),
            @"chuckReviewCount = LeaderboardBoards\.BoardSize \+ (\d+)",
            "the showcase post");

        var coverage = Offset(
            Read(Seeder, "DataSeeder.Leaderboards.cs"),
            @"target = LeaderboardBoards\.BoardSize \+ (\d+)",
            "leaderboard coverage");

        showcase.Should().BeGreaterThan(coverage,
            "the widget takes the highest sum in the week, and coverage tops one post at " +
            $"{LeaderboardBoards.BoardSize + coverage} by construction. Level with it, which " +
            "post wins is whatever the query plan returns first");
    }

    /// <summary>The number added to the board size in one source.</summary>
    private static int Offset(string source, string pattern, string what)
    {
        var found = Regex.Match(source, pattern);
        found.Success.Should().BeTrue(
            $"{what} derives its score from LeaderboardBoards.BoardSize; written out as a " +
            "number it stops moving when the board does");
        return int.Parse(found.Groups[1].Value);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, Path.Combine(parts)));

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
