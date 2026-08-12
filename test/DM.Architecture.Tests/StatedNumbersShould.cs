using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A number the owner of a game typed in is not recalculated behind their back.
/// </summary>
/// <remarks>
/// RecruitmentPcLimit is what a master announces: "I am taking five players".
/// Nothing reads it as a gate — whether recruitment is open is its own flag — so
/// the number exists to be read beside the count of active characters.
///
/// Storage kept it consistent in both directions. Raising it when the roster
/// outgrew it has a reason: a stated three beside six active characters is a
/// state no reader can make sense of. Lowering it had none, and it was done by
/// arithmetic — Math.Max(activeCount, limit - 1) — every time somebody left. A
/// game recruiting five began claiming four the moment one player retired, with
/// nobody asked and nobody told.
///
/// The rule is written as a search over the source because that is the shape the
/// defect had: no test covered it, and none would have, since the behaviour
/// lived inside a private method of a repository and was correct in every
/// direction the tests happened to look. What can be checked cheaply and
/// exactly is that the assignment never derives the new value by subtracting
/// from the old one.
/// </remarks>
public class StatedNumbersShould
{
    /// <summary>
    /// Values a person enters and the system must keep verbatim until the same
    /// person changes them.
    /// </summary>
    private static readonly string[] StatedByTheOwner = { "RecruitmentPcLimit" };

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static IEnumerable<string> SourceFiles() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));

    /// <summary>An assignment whose right-hand side subtracts from the field itself.</summary>
    private static Regex SelfDecrement(string field) => new(
        $@"{field}\s*=\s*[^;\r\n]*{field}[^;\r\n]*-\s*\d",
        RegexOptions.Compiled);

    [Fact]
    public void NotBeLoweredByTheSystemThatStoresThem()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            var source = File.ReadAllText(file);
            foreach (var field in StatedByTheOwner)
            {
                if (!source.Contains(field, StringComparison.Ordinal))
                {
                    continue;
                }

                var found = SelfDecrement(field).Match(source);
                if (!found.Success)
                {
                    continue;
                }

                var line = source[..found.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetRelativePath(RepositoryRoot, file)}:{line}: {found.Value.Trim()}");
            }
        }

        offenders.Should().BeEmpty(
            "the number belongs to the person who typed it, and lowering it silently makes " +
            "the site announce something its owner never said");
    }

    [Fact]
    public void HaveASearchThatWouldFindOne()
    {
        // The rule is a scan, and a scan that stops matching passes in silence.
        var rule = SelfDecrement("RecruitmentPcLimit");

        rule.IsMatch("game.RecruitmentPcLimit = Math.Max(activeCount, game.RecruitmentPcLimit.Value - 1);")
            .Should().BeTrue("this is the assignment the rule exists for");
        rule.IsMatch("game.RecruitmentPcLimit = activeCount;")
            .Should().BeFalse("raising the number to the live count is the one change that has a reason");
        rule.IsMatch("game.RecruitmentPcLimit = updateGame.RecruitmentPcLimit;")
            .Should().BeFalse("the owner setting their own number is the point");
    }

    [Fact]
    public void FindTheFilesToSearch() =>
        SourceFiles().Should().HaveCountGreaterThan(200,
            "a file walk that stops matching turns the rule above green by checking nothing");
}
