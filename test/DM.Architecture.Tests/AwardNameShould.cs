using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The award is called "Народное признание, например", and every quotation of it
/// carries the whole name.
/// </summary>
/// <remarks>
/// The last two words are a joke and part of the title in the seed. Three comments
/// quoted the name without them, which is exactly how such a joke gets "fixed": the
/// next reader compares the seed against a comment and takes the difference for a
/// typo. A comment cannot be checked mechanically in general - this one can.
/// </remarks>
public class AwardNameShould
{
    private const string Name = "Народное признание";
    private const string Tail = ", например";

    private static readonly string[] Extensions = [".cs", ".ts", ".vue"];

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    [Fact]
    public void BeQuotedWholeWhereverItIsQuoted()
    {
        var truncated = new List<string>();

        var files = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.*", SearchOption.AllDirectories)
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
