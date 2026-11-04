using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The seed and the client mean the same thing by "за неделю".
/// </summary>
/// <remarks>
/// The seeder places the showcase inside the client's window and the coverage outside it. A rolling seven
/// days: a calendar boundary empties the block on Mondays and on old fixtures.
/// </remarks>
public class WeekWindowShould
{
    private const int Days = 7;

    [Fact]
    public void BeSevenRollingDaysOnTheClient()
    {
        var source = RepositoryFiles.Read("src", "DM.Web.Client", "src", "shared", "lib", "utils", "datetime.ts");
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
        var source = RepositoryFiles.Read("src", "DM.Tools.Seeder", "Seeding", "DataSeeder.cs");
        var body = SourceText.Between(source, "private static DateTimeOffset WeekStartUtc", ";");

        body.Should().NotContain("DayOfWeek",
            "the seeder placed its showcase post against a calendar boundary the client " +
            "no longer uses, and a fixture older than that boundary shows no best post");
        body.Should().Contain($"AddDays(-{Days})",
            "the seeder and the client have to name the same window, or the post the seed " +
            "puts in it is not the post the block reads");
    }
}
