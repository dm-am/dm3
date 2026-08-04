using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A newbie is one number, and the stored column agrees with it.
/// </summary>
/// <remarks>
/// The rule "fewer than a hundred game posts" was written out seven times over two
/// different sources - the denormalised counter and a COUNT over posts, which the
/// removal of a post was already enough to split - and the number existed once more
/// as a setting the schema cannot read. Lowering the setting moved game
/// premoderation, reviews and endorsements while the badge on the profile, the user
/// filter and the assistant lists stayed on the column's hundred; the only thing
/// guarding that was a warning line at warm-up, after which the site started anyway.
///
/// Read out of the sources rather than restated here: the migration and the two
/// snapshots are what a database is built from, and a number is compared rather
/// than a phrase, so a failure says which copy moved.
/// </remarks>
public class NewbieThresholdShould
{
    /// <summary>
    /// Walks up from the test binary to the repository root. The sources are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, Path.Combine(parts)));

    /// <summary>The SQL the column computes, as the sources spell it.</summary>
    private static string Expression =>
        $"\\\"QuantityRating\\\" < {ProbationPolicy.NewbiePostThreshold}";

    [Fact]
    public void BeTheNumberTheStoredColumnComputes()
    {
        Read("src", "DM.Infrastructure.Persistence", "DmDbContext.cs")
            .Should().Contain($".HasComputedColumnSql(\"{Expression}\", stored: true)",
                "the model is where the column is declared");

        Read("src", "DM.Infrastructure.Persistence", "Migrations", "20260729122733_InitialCreate.cs")
            .Should().Contain($"computedColumnSql: \"{Expression}\"",
                "the migration is what the database is actually built from");

        foreach (var snapshot in new[]
                 {
                     "DmDbContextModelSnapshot.cs",
                     "20260729122733_InitialCreate.Designer.cs"
                 })
        {
            Read("src", "DM.Infrastructure.Persistence", "Migrations", snapshot)
                .Should().Contain($".HasComputedColumnSql(\"{Expression}\", true)",
                    $"{snapshot} describes the same column, and a snapshot disagreeing with the " +
                    "migration is a schema nobody can rebuild");
        }
    }

    /// <summary>
    /// No second spelling of the rule. Only the schema repeats the number, and the
    /// test above is what holds it to the constant.
    /// </summary>
    /// <remarks>
    /// The schema is four files, not one: the model declares the column, and the
    /// migration and the two snapshots are the generated text a database is built
    /// from. They are excluded here and compared to the constant above instead.
    ///
    /// The comparison is matched through the escaped quotes the SQL carries. A
    /// pattern that only accepts whitespace after the column name reads as strict
    /// and passes everything, which is how the number came back into the model
    /// unnoticed.
    /// </remarks>
    [Fact]
    public void BeSpelledNowhereElse()
    {
        var rule = new Regex(@"QuantityRating(\\?"")?\s*[<>]=?\s*\d", RegexOptions.Compiled);

        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                       StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal)
                       && Path.GetFileName(path) != "DmDbContext.cs")
            .Where(path => rule.IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "the predicate lives on ProbationPolicy.IsNewbie and the number on the constant " +
            "beside it; a copy of either answers differently the first time the product rule moves");
    }
}
