using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The seed and the running host mean the same thing by "active".
/// </summary>
/// <remarks>
/// The popularity score counts users who were active recently, and how recent that is
/// is a product decision several screens derive from: listing filters, subscriber
/// ordering, the score itself. It is held in one constant precisely so that changing
/// it is one edit, and the constant says so in its own remarks.
///
/// The seeder wrote the window out as a literal instead. The two agreed by accident,
/// which is the worst state for a duplicate to be in: nothing was wrong, and the first
/// change to the product rule would have left a fixture whose scores disagree with the
/// screens that read them, with no test between the edit and the disagreement.
/// </remarks>
public class PopularityWindowShould
{
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }

    [Fact]
    public void BeTheProductActivePeriodInTheSeeder()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "DM.Tools.Seeder", "Seeding", "DataSeeder.Leaderboards.cs"));
        var body = Between(source, "private async Task UpdatePopularityScores", "\n    }");

        body.Should().Contain("ActivityPolicy.ActivePeriod",
            "the seeder references the domain kernel already, so taking the window from " +
            "it costs nothing and keeps one definition of the word");
        body.Should().NotContain("TimeSpan.From",
            "a window spelled here is a second definition of \"active\", and it drifts " +
            "silently the first time the product one moves");
    }

    /// <summary>
    /// Text between an opening marker and the first terminator after it: enough to
    /// read one method, and loud when the method is gone or renamed.
    /// </summary>
    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, $"the source must still declare {start}");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        to.Should().BeGreaterThan(-1, $"the declaration of {start} must be terminated");
        return source[from..to];
    }
}
