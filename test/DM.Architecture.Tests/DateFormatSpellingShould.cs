using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

// Asserted against the sources: the defect is the string in more than one place.
public class DateFormatSpellingShould
{
    // Where the patterns are declared, and the only file allowed to spell them.
    private const string Home = "src/DM.Web.Client/src/shared/lib/utils/datetime.ts";

    /// <summary>
    /// A dayjs date pattern in code: the day-month head is what every spelling
    /// the site uses starts with, and quotes are what makes it a literal rather
    /// than prose about one.
    /// </summary>
    private static readonly Regex Literal = new(
        @"[""'`]DD\.MM[^""'`]*[""'`]", RegexOptions.Compiled);


    [Fact]
    public void SpellTheDatePatternInOnePlace()
    {
        var root = RepositoryLayout.Root;
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
                // A comment naming the format is prose, not the format.
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
