using System;
using System.IO;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The seed and the client mean the same thing by "за неделю".
/// </summary>
/// <remarks>
/// The homepage block "лучший пост недели" filters posts by a window the client
/// computes, and the seeder places its showcase post inside that window while
/// keeping the leaderboard coverage outside it. The two are written in different
/// languages and nothing links them, so when one moved the block went silently
/// empty on the site while every test stayed green.
///
/// It is a rolling seven days rather than the calendar Monday. A calendar
/// boundary empties the block for the first hours of every Monday, and empties
/// it for good once a fixture is more than a week old, which a fixture on a
/// developer machine always becomes.
/// </remarks>
public class WeekWindowShould
{
    private const int Days = 7;

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, Path.Combine(parts)));

    [Fact]
    public void BeSevenRollingDaysOnTheClient()
    {
        var source = Read("src", "DM.Web.Client", "src", "shared", "lib", "utils", "datetime.ts");
        var body = SourceText.Between(source, "export function getWeekStartUtc()", "\n}");

        body.Should().NotContain("getUTCDay",
            "a calendar boundary leaves the block empty every Monday morning");
        Regex.IsMatch(body, $@"{Days}\s*\*\s*24\s*\*\s*60\s*\*\s*60\s*\*\s*1000").Should().BeTrue(
            $"the window is {Days} days back from now, and the seeder places its showcase " +
            "post by the same measure");
    }

    [Fact]
    public void BeTheSameSevenDaysInTheSeeder()
    {
        var source = Read("src", "DM.Tools.Seeder", "Seeding", "DataSeeder.cs");
        var body = SourceText.Between(source, "private static DateTimeOffset WeekStartUtc", ";");

        body.Should().NotContain("DayOfWeek",
            "the seeder placed its showcase post against a calendar boundary the client " +
            "no longer uses, and a fixture older than that boundary shows no best post");
        body.Should().Contain($"AddDays(-{Days})",
            "the seeder and the client have to name the same window, or the post the seed " +
            "puts in it is not the post the block reads");
    }

    /// <summary>
    /// Text between an opening marker and the first terminator after it. Enough
    /// to read one small function, and it fails loudly when the function is gone.
    /// </summary>
}
