using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A parameter written as a table row lives in one document.
/// </summary>
/// <remarks>
/// Copying a table is how two documents come to answer one question two ways.
/// The token parameters and the password policy stood both in the security
/// requirements and in the description of the login flow, and the attack
/// checklist, copied the same way, had already lost two rows in one of the
/// copies: the reader of the shorter one had no way to learn that a longer one
/// existed. Whether two paragraphs say the same thing cannot be checked
/// mechanically. Whether a row is written twice can, and a row of parameters is
/// exactly what gets changed in one place and read from the other.
///
/// Heading rows are left alone. Two tables answering different questions share
/// the same pair of column names without either of them lying.
/// </remarks>
public class DocumentedTablesShould
{
    /// <summary>The line that stands under the heading of a table.</summary>
    private static readonly Regex Separator = new(@"^\|[\s:|-]+\|$", RegexOptions.Compiled);

    private static DirectoryInfo RepositoryRoot => DM.Testing.RepositoryLayout.RootDirectory;

    private static bool IsRow(string line) =>
        line.StartsWith('|') && line.EndsWith('|') && !Separator.IsMatch(line);

    /// <summary>The heading of a table: the separator is the line under it.</summary>
    private static bool IsHeading(IReadOnlyList<string> lines, int index) =>
        index + 1 < lines.Count && Separator.IsMatch(lines[index + 1]);

    private static string[] Cells(string line) =>
        line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    [Fact]
    public void KeepEveryRowInOneDocument()
    {
        var root = RepositoryRoot;
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
