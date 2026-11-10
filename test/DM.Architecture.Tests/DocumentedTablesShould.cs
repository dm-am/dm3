using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

// A row of parameters gets changed in one copy and read from the other.
public class DocumentedTablesShould
{
    private static readonly Regex Separator = new(@"^\|[\s:|-]+\|$", RegexOptions.Compiled);


    private static bool IsRow(string line) =>
        line.StartsWith('|') && line.EndsWith('|') && !Separator.IsMatch(line);

    private static bool IsHeading(IReadOnlyList<string> lines, int index) =>
        index + 1 < lines.Count && Separator.IsMatch(lines[index + 1]);

    private static string[] Cells(string line) =>
        line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    [Fact]
    public void KeepEveryRowInOneDocument()
    {
        var root = RepositoryLayout.RootDirectory;
        var documents = Directory.GetFiles(
            Path.Combine(root.FullName, "docs"), "*.md", SearchOption.AllDirectories);

        documents.Should().NotBeEmpty("the conventions and guides live under docs/");

        var rows = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            var lines = File.ReadAllLines(document).Select(line => line.Trim()).ToArray();

            for (var i = 0; i < lines.Length; i++)
            {
                if (!IsRow(lines[i]) || IsHeading(lines, i))
                {
                    continue;
                }

                var cells = Cells(lines[i]);
                if (cells.Length < 2)
                {
                    continue;
                }

                var key = string.Join(" | ", cells).ToLowerInvariant();
                var place =
                    $"{Path.GetRelativePath(root.FullName, document).Replace('\\', '/')}:{i + 1}";

                if (!rows.TryGetValue(key, out var places))
                {
                    rows[key] = places = new List<string>();
                }

                places.Add(place);
            }
        }

        // A walk that parsed nothing would leave the assertion below empty.
        rows.Should().NotBeEmpty("the documents state their parameters in tables");

        var copied = rows
            .Where(row => row.Value
                .Select(place => place[..place.LastIndexOf(':')])
                .Distinct(StringComparer.Ordinal)
                .Count() > 1)
            .Select(row => $"{row.Key} -> {string.Join(", ", row.Value)}")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        copied.Should().BeEmpty(
            "a parameter copied into a second document is changed in one of them and read from " +
            "the other, and the document that does not own the rule links to the one that does");
    }
}
