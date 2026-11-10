using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// RecruitmentPcLimit is what a master announces and gates nothing. Searched in the source: the new value
/// is never derived by subtracting from the old one.
/// </remarks>
public class StatedNumbersShould
{
    /// <summary>
    /// Values a person enters and the system must keep verbatim until the same
    /// person changes them.
    /// </summary>
    private static readonly string[] StatedByTheOwner = { "RecruitmentPcLimit" };


    private static IEnumerable<string> SourceFiles() => Directory
        .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));

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
                offenders.Add($"{Path.GetRelativePath(RepositoryLayout.Root, file)}:{line}: {found.Value.Trim()}");
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
