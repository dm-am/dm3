using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

// Read out of the sources: the migration and snapshots build the database; a number names the copy
// that moved.
public class NewbieThresholdShould
{

    /// <summary>The SQL the column computes, as the sources spell it.</summary>
    private static string Expression =>
        $"\\\"QuantityRating\\\" < {ProbationPolicy.NewbiePostThreshold}";

    [Fact]
    public void BeTheNumberTheStoredColumnComputes()
    {
        SchemaSources.ModelText
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

    // Matched through the escaped quotes the SQL carries.
    [Fact]
    public void BeSpelledNowhereElse()
    {
        var model = SchemaSources.ModelFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = new[]
        {
            new Regex(@"QuantityRating(\\?"")?\s*[<>]=?\s*\d", RegexOptions.Compiled),
            new Regex(@"[<>]=?\s*ProbationPolicy\.NewbiePostThreshold|NewbiePostThreshold\s*[<>]=?",
                RegexOptions.Compiled)
        };

        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                       StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal))
            .Where(path => !model.Contains(Path.GetFullPath(path)))
            .Where(path => Path.GetFileName(path) != "ProbationPolicy.cs")
            .Where(path => rules.Any(rule => rule.IsMatch(File.ReadAllText(path))))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "the predicate lives on ProbationPolicy.IsNewbie and the number on the constant " +
            "beside it; a copy of either answers differently the first time the product rule moves");
    }

    // An XML doc cannot interpolate a constant, so its number is held to the constant here.
    [Fact]
    public void BeTheNumberTheApiDocumentationPromises()
    {
        var mention = new Regex(@"(?<number>\d+)\s+(game posts|постов)", RegexOptions.Compiled);

        var stale = RepositoryFiles.ProductionSources()
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
    /// A COUNT over Posts differs from QuantityRating once a post is removed. Matched on the
    /// implementations, not the declarations.
    /// </remarks>
    [Fact]
    public void BeCountedOffTheSameColumnInEveryRepository()
    {
        var implementation = new Regex(
            @"GetUserPostCountAsync\s*\([^)]*\)\s*(=>|\{)(?<body>[\s\S]*?);",
            RegexOptions.Compiled);

        var implementations = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*Repository.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
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
