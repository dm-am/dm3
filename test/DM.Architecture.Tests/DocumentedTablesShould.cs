using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A parameter written as a table row lives in one document, and a table
/// promised a size has it.
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
///
/// The second rule reads the migration plan, where each rename group promises
/// its size and the summary promises their sum. Those numbers are the checksum
/// of a one-way conversion: the run is verified against "expect this many
/// renames". The summary said 74 while the groups listed 81, so the check would
/// have been made against a number nothing produces, and a list cut down to the
/// summary would have left seven accounts carrying names the new rules reject.
/// </remarks>
public class DocumentedTablesShould
{
    /// <summary>The line that stands under the heading of a table.</summary>
    private static readonly Regex Separator = new(@"^\|[\s:|-]+\|$", RegexOptions.Compiled);

    /// <summary>A rename group of the migration plan, and the size it promises.</summary>
    private static readonly Regex GroupHeading = new(
        @"^### Группа [A-Z]:.*\((\d+)\)$", RegexOptions.Compiled);

    /// <summary>The number a statistics cell opens with, thousands and all.</summary>
    private static readonly Regex Amount = new(@"^([\d,]+)", RegexOptions.Compiled);

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

    [Fact]
    public void AddUpTheRenamesTheMigrationPlanPromises()
    {
        var root = RepositoryRoot;
        var path = Path.Combine(root.FullName, "docs", "plans", "DM2_MIGRATION.md");
        File.Exists(path).Should().BeTrue("the conversion plan is where these numbers live");

        var lines = File.ReadAllLines(path).Select(line => line.Trim()).ToArray();

        var groups = new List<(string Heading, int Promised, int Rows)>();
        var statistics = new Dictionary<string, int>(StringComparer.Ordinal);

        string? heading = null;
        var promised = 0;
        var rows = 0;

        void CloseGroup()
        {
            var name = heading;
            if (name != null)
            {
                groups.Add((name, promised, rows));
            }

            heading = null;
            rows = 0;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            var group = GroupHeading.Match(line);
            if (group.Success)
            {
                CloseGroup();
                heading = line;
                promised = int.Parse(group.Groups[1].Value, CultureInfo.InvariantCulture);
                continue;
            }

            if (line.StartsWith('#'))
            {
                CloseGroup();
                continue;
            }

            if (!IsRow(line) || IsHeading(lines, i))
            {
                continue;
            }

            if (heading != null)
            {
                rows++;
                continue;
            }

            var cells = Cells(line);
            if (cells.Length != 2)
            {
                continue;
            }

            var amount = Amount.Match(cells[1]);
            if (amount.Success)
            {
                statistics[cells[0]] = int.Parse(
                    amount.Groups[1].Value.Replace(",", string.Empty, StringComparison.Ordinal),
                    CultureInfo.InvariantCulture);
            }
        }

        CloseGroup();

        groups.Should().HaveCountGreaterThan(1, "the plan splits the renames into groups");

        foreach (var (name, size, counted) in groups)
        {
            counted.Should().Be(size, $"the heading promises the size of its table: {name}");
        }

        int Statistic(string parameter)
        {
            statistics.ContainsKey(parameter).Should().BeTrue(
                $"the statistics table names \"{parameter}\"");
            return statistics[parameter];
        }

        var renamed = Statistic("Требуют переименования");
        var compliant = Statistic("Соответствуют новым правилам");
        var total = Statistic("Всего пользователей");

        renamed.Should().Be(groups.Sum(group => group.Promised),
            "the conversion is verified against this number and the groups below are what " +
            "produces it, so a list longer than the summary leaves accounts unrenamed");

        (compliant + renamed).Should().Be(total,
            "an account is either renamed or left as it is");
    }
}
