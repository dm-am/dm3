using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The pattern a date is rendered with is written in one file.
/// </summary>
/// <remarks>
/// It used to be a literal at the call site, seventeen times across nine files,
/// and a literal repeated that many times is not a format but a coincidence: one
/// copy in the events panel said "DD.MM" where its neighbour eleven lines down
/// said "DD.MM.YYYY", and nothing in the file says which of the two the panel
/// meant. Changing how the site spells a date meant finding all seventeen, and
/// the eighteenth was written the day after.
///
/// Asserted against the sources rather than through a render: the defect is that
/// the string exists in more than one place, which no rendered output shows.
/// </remarks>
public class DateFormatSpellingShould
{
    /// <summary>Where the patterns are declared, and the only file allowed to spell them.</summary>
    private const string Home = "src/DM.Web.Client/src/shared/lib/utils/datetime.ts";

    /// <summary>
    /// A dayjs date pattern in code: the day-month head is what every spelling
    /// the site uses starts with, and quotes are what makes it a literal rather
    /// than prose about one.
    /// </summary>
    private static readonly Regex Literal = new(
        @"[""'`]DD\.MM[^""'`]*[""'`]", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    [Fact]
    public void SpellTheDatePatternInOnePlace()
    {
        var root = RepositoryRoot;
        var client = Path.Combine(root, "src", "DM.Web.Client", "src");

        var home = Path.Combine(root, Home.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(home).Should().BeTrue($"{Home} is where the patterns live");
        Literal.IsMatch(File.ReadAllText(home)).Should().BeTrue(
            "the rule is that the pattern is written here, so a rule finding it nowhere " +
            "checks nothing");

        var offenders = new List<string>();
        foreach (var path in Directory
                     .EnumerateFiles(client, "*.*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".ts", StringComparison.Ordinal) ||
                                    path.EndsWith(".vue", StringComparison.Ordinal))
                     .Where(IsAuthored))
        {
            if (string.Equals(path, home, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var line in File.ReadAllLines(path))
            {
                // Prose describing the format is not the format. A comment naming
                // it is how the reader learns what a call renders, and forbidding
                // that would only move the duplication out of reach of this rule.
                var code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal) ||
                    code.StartsWith("*", StringComparison.Ordinal) ||
                    code.StartsWith("/*", StringComparison.Ordinal)) continue;

                if (Literal.IsMatch(line))
                {
                    offenders.Add(Path.GetRelativePath(root, path) + ": " + line.Trim());
                }
            }
        }

        offenders.Should().BeEmpty(
            "the pattern is exported from datetime.ts and imported where it is needed; a " +
            "literal at the call site is the copy that drifts from the others without " +
            "anything saying so");
    }

    private static bool IsAuthored(string path) =>
        !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "node_modules" or "dist" or "coverage");
}
