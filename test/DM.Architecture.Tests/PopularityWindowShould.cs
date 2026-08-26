using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The seed and the running host mean the same thing by "popular".
/// </summary>
/// <remarks>
/// The popularity score counts users who were active recently, and how recent
/// that is is a product decision several screens derive from: listing filters,
/// subscriber ordering, the score itself. It is held in one constant precisely
/// so that changing it is one edit, and the constant says so in its own remarks.
///
/// The seeder wrote the window out as a literal instead, under a copy of the
/// whole calculation - the same queries, line for line, in a background job of
/// the HTTP host and in the fixture. The two agreed by accident, which is the
/// worst state for a duplicate to be in: nothing was wrong, and the first change
/// to the product rule would have left a fixture whose scores disagree with the
/// screens that read them, with no test between the edit and the disagreement.
///
/// So the rule is no longer "the seeder spells the constant" but "there is one
/// calculation": the definition lives in the two processors, both callers go
/// through them, and neither the callers nor the processors spell a window of
/// their own.
/// </remarks>
public class PopularityWindowShould
{
    /// <summary>The two places the definition is allowed to be.</summary>
    private static readonly string[] Processors =
    [
        "src/DM.Domain.Game/Features/Popularity/GamePopularityProcessor.cs",
        "src/DM.Domain.Blog/Features/Popularity/BlogPopularityProcessor.cs",
    ];

    /// <summary>Everything that asks for a recalculation.</summary>
    private static readonly (string File, string Method)[] Callers =
    [
        ("src/DM.Tools.Seeder/Seeding/DataSeeder.Leaderboards.cs", "private async Task UpdatePopularityScores"),
        ("src/DM.Web.API/HostedServices/PopularityScoreService.cs", "protected override async Task RunOnce"),
    ];

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    [Fact]
    public void TakeTheActiveWindowFromTheProductConstant()
    {
        foreach (var processor in Processors)
        {
            var source = File.ReadAllText(Path.Combine(RepositoryRoot, processor));

            source.Should().Contain("ActivityPolicy.ActivePeriod",
                $"{processor} holds the definition of a popular game or blog, and the word " +
                "\"active\" in it is the site's, not this file's");
            source.Should().NotContain("TimeSpan.From",
                $"a window spelled in {processor} is a second definition of \"active\", and " +
                "it drifts silently the first time the product one moves");
        }
    }

    [Fact]
    public void LeaveTheCalculationToTheProcessorsInEveryCaller()
    {
        var root = RepositoryRoot;
        var offenders = new List<string>();

        foreach (var (file, method) in Callers)
        {
            var body = SourceText.Between(File.ReadAllText(Path.Combine(root, file)), method, "\n    }");

            if (!body.Contains("PopularityProcessor", StringComparison.Ordinal) &&
                !body.Contains("UpdateScoresAsync", StringComparison.Ordinal))
            {
                offenders.Add($"{file}: does not go through a popularity processor");
            }

            if (body.Contains("ActivityPolicy", StringComparison.Ordinal) ||
                body.Contains("TimeSpan.From", StringComparison.Ordinal))
            {
                offenders.Add($"{file}: decides for itself what \"active\" means");
            }
        }

        offenders.Should().BeEmpty(
            "one definition of the score, called from the host and from the seeder; a " +
            "caller that computes it again is the duplicate this closed coming back");
    }

    /// <summary>
    /// Text between an opening marker and the first terminator after it: enough to
    /// read one method, and loud when the method is gone or renamed.
    /// </summary>
}
