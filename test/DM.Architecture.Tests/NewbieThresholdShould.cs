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
    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

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

        SchemaSources.Migration
            .Should().Contain($"computedColumnSql: \"{Expression}\"",
                "the migration is what the database is actually built from");

        foreach (var (snapshot, text) in SchemaSources.Snapshots)
        {
            text
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
        var rules = new[]
        {
            new Regex(@"QuantityRating(\\?"")?\s*[<>]=?\s*\d", RegexOptions.Compiled),
            new Regex(@"[<>]=?\s*ProbationPolicy\.NewbiePostThreshold|NewbiePostThreshold\s*[<>]=?",
                RegexOptions.Compiled)
        };

        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                       StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal)
                       && Path.GetFileName(path) != "DmDbContext.cs")
            .Where(path => Path.GetFileName(path) != "ProbationPolicy.cs")
            .Where(path => rules.Any(rule => rule.IsMatch(File.ReadAllText(path))))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "the predicate lives on ProbationPolicy.IsNewbie and the number on the constant " +
            "beside it; a copy of either answers differently the first time the product rule moves");
    }

    /// <summary>
    /// The number the published documentation states is the number the code uses.
    /// </summary>
    /// <remarks>
    /// The endorsement and post-review endpoints spell the threshold out in their
    /// XML docs, which is what a client reads in Swagger before deciding whether an
    /// account qualifies. An XML doc cannot interpolate a constant, so the wording
    /// is left alone and held to the constant from here instead: moving the product
    /// rule without moving the sentence leaves the API promising the old number,
    /// and there is nothing else in the build that would notice.
    ///
    /// Refusal messages are not matched - they interpolate the constant and carry
    /// no digits of their own, which is the shape this test is asking the rest of
    /// the prose to reach.
    /// </remarks>
    [Fact]
    public void BeTheNumberTheApiDocumentationPromises()
    {
        var mention = new Regex(@"(?<number>\d+)\s+(game posts|постов)", RegexOptions.Compiled);

        var stale = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                       StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal))
            .SelectMany(path => mention
                .Matches(File.ReadAllText(path))
                .Select(match => (File: Path.GetFileName(path), Number: match.Groups["number"].Value)))
            .Where(pair => pair.Number != ProbationPolicy.NewbiePostThreshold.ToString())
            .Select(pair => $"{pair.File}: {pair.Number}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToArray();

        stale.Should().BeEmpty(
            $"the threshold is {ProbationPolicy.NewbiePostThreshold}, and a documented number " +
            "that disagrees tells a client the account is eligible when the endpoint refuses it");
    }

    /// <summary>
    /// One number is not enough: the count it is compared against comes from one
    /// column too.
    /// </summary>
    /// <remarks>
    /// "Game posts" had two readings - the denormalised QuantityRating the profile
    /// badge and the user filter are built on, and a COUNT over Posts, which the
    /// global !IsRemoved filter makes a different number the moment a post is
    /// deleted. Three repositories answered the same question and one of them
    /// counted, so a user could be shown the newbie badge and still be allowed to
    /// review a game, or the reverse.
    ///
    /// Matched on the implementations rather than the declarations: the interfaces
    /// spell the method without a body, and a rule stated on the name alone would
    /// be satisfied by an interface nobody implements the same way.
    /// </remarks>
    [Fact]
    public void BeCountedOffTheSameColumnInEveryRepository()
    {
        var implementation = new Regex(
            @"GetUserPostCountAsync\s*\([^)]*\)\s*(=>|\{)(?<body>[\s\S]*?);",
            RegexOptions.Compiled);

        var implementations = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*Repository.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                       StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal))
            .SelectMany(path => implementation
                .Matches(File.ReadAllText(path))
                .Select(match => (File: Path.GetFileName(path), Body: match.Groups["body"].Value)))
            .ToArray();

        implementations.Should().NotBeEmpty(
            "the eligibility repositories are what this rule is about, and an empty match " +
            "would report a guard that is not there");

        implementations
            .Where(pair => !pair.Body.Contains("QuantityRating", StringComparison.Ordinal))
            .Select(pair => pair.File)
            .Should().BeEmpty(
                "QuantityRating is the counter the stored IsNewbie column computes from, and a " +
                "COUNT over Posts is a different number for anyone whose post was deleted");
    }
}
