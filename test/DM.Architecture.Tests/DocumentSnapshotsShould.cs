using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A hand-written document states conventions, not a snapshot of the tree: no
/// dates, and no counts of the things the next change adds or removes.
/// </summary>
/// <remarks>
/// Dates in both spellings (26.09.2026, 2026-08-25) outside quotes and code, and counts: a number
/// before a noun the tree has more or fewer of with every change, or after "всего".
/// </remarks>
public class DocumentSnapshotsShould
{
    private const string Date = "date";
    private const string Count = "count";

    private static readonly Regex[] Dates =
    [
        new(@"(?<![\d.])\d{1,2}\.\d{1,2}\.\d{4}(?!\d)", RegexOptions.Compiled),
        new(@"(?<![\d-])\d{4}-\d{2}-\d{2}(?![\d-])", RegexOptions.Compiled),
    ];

    /// <summary>
    /// One pattern rather than two, so that "всего 12 тестов" is one count: the
    /// leftmost match takes the phrase whole.
    /// </summary>
    private static readonly Regex[] Counts =
    [
        new(@"(?<![\p{L}\d.,:-])(?:[Вв]сего\s+)?(?:~\s*)?\d+\+?\s+(?:тест|файл|иконк|иконок|кадр|эндпоинт|endpoint|" +
            @"маршрут|роут|компонент|страниц|окон|окна|запис|мест|гейт|скилл|агент|находк|находок|" +
            @"test|file|icon|route|component|page|screen|frame)\p{L}*|(?<!\p{L})[Вв]сего\s+\d+",
            RegexOptions.Compiled),
    ];

    /// <summary>A quoted example: a run of text between straight quotes on one line.</summary>
    private static readonly Regex Quoted = new("\"[^\"\n]*\"", RegexOptions.Compiled);

    [Fact]
    public void StateNoDayAndNoSizeOfTheTree()
    {
        var documents = HandwrittenDocuments.All();
        documents.Should().HaveCountGreaterThan(30, "a rule that reads a handful of documents checks little");

        var snapshots = new List<string>();
        foreach (var document in documents)
        {
            var prose = document.Lines.Where(line => line.IsProse).Select(line => Prose(line.Text)).ToList();
            foreach (var (kind, patterns) in new[] { (Date, Dates), (Count, Counts) })
            {
                var found = prose.Sum(text => patterns.Sum(pattern => pattern.Matches(text).Count));
                if (found > 0)
                {
                    snapshots.Add($"{document.Path} :: {kind}: {found}");
                }
            }
        }

        string.Join(Environment.NewLine, snapshots).Should().BeEmpty(
            "a document states the rule and the reason, not the day or the size of the tree: leave the date " +
            "to git log and the count to the code or docs/STATUS.md");
    }

    /// <summary>What is a snapshot and what is a rule or an example.</summary>
    [Theory]
    [InlineData("правило владельца от 26.09.2026", Date, 1)]
    [InlineData("| аудит 2026-08-24 |", Date, 1)]
    [InlineData("причина: \"Не раньше 05.10.2026\"", Date, 0)]
    [InlineData("формат `DD.MM.YYYY`, пример `14.03.2026`", Date, 0)]
    [InlineData("Проект средний (~40 иконок)", Count, 1)]
    [InlineData("Всего 168 в 44 разделах: 93 страницы, 60 окон", Count, 3)]
    [InlineData("всего 12 тестов", Count, 1)]
    [InlineData("длина 2-20 символов, 3 окно со словом", Count, 0)]
    [InlineData("Посторонний 401 окно входа не трогает", Count, 0)]
    public void TellASnapshotFromARule(string line, string kind, int count)
    {
        var text = Prose(line);
        (kind == Date ? Dates : Counts).Sum(pattern => pattern.Matches(text).Count).Should().Be(count);
    }

    private static string Prose(string line) => Quoted.Replace(HandwrittenDocuments.WithoutCodeSpans(line), " ");
}
