using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

// The last two words are a joke and part of the title; a comment quoting it short reads as a typo.
public class AwardNameShould
{
    private const string Name = "Народное признание";
    private const string Tail = ", например";

    private static readonly string[] Extensions = [".cs", ".ts", ".vue"];


    [Fact]
    public void BeQuotedWholeWhereverItIsQuoted()
    {
        var truncated = new List<string>();

        var files = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                       StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            for (var at = text.IndexOf(Name, StringComparison.Ordinal);
                 at >= 0;
                 at = text.IndexOf(Name, at + Name.Length, StringComparison.Ordinal))
            {
                if (!text[(at + Name.Length)..].StartsWith(Tail, StringComparison.Ordinal))
                {
                    truncated.Add(Path.GetFileName(file));
                }
            }
        }

        truncated.Should().BeEmpty(
            "the trailing words are part of the award's name, and a quotation without them " +
            "reads as a typo in the seed to whoever compares the two");
    }
}
