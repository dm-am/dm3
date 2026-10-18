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
/// The session instructions under .claude put it as a principle of the
/// documentation: conventions, not a snapshot of the state. Lists of files,
/// names of classes, numbers of tests, sizes of the seed and dated summaries of
/// what is done go stale with the next wave of edits. A count of icons or files
/// is right on the day it is typed, and a date says when somebody looked rather
/// than what holds. What a count would say, the tree says (docs/STATUS.md counts
/// it, by script), and when something was decided is what git log keeps.
///
/// Dates are counted in both spellings the documents use, 26.09.2026 and
/// 2026-08-25. A table gets no exemption: no hand-written document keeps a
/// history table, the dated rows found are decisions and audits in passing, and
/// history is what the log is for. A date inside straight quotes is left alone,
/// because there it is an example of what a screen shows ("Не раньше 05.10.2026"),
/// not a claim about when; so is anything in code.
///
/// A count is a number before a noun naming something the tree has more or fewer
/// of with every change (tests, files, icons, frames, routes, pages, windows,
/// entries, places, gates, skills, agents, findings), with an optional "~" before
/// it, or a number after "всего". The list is kept to such nouns: "2-20 символов"
/// or "3 окно со словом" are rules, and a rule may carry a number.
///
/// Recorded per document and kind at its count (see
/// <see cref="HandwrittenDocuments"/> for the contract of the register).
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

    /// <summary>A document holding snapshots, at its count of one kind.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Kind">A date or a count.</param>
    /// <param name="Count">How many the document holds.</param>
    private sealed record Recorded(string File, string Kind, int Count);

    private static readonly Recorded[] Register =
    [
        new("docs/SURFACES.md", "count", 4),
        new("docs/conventions/CODE_STYLE.md", "count", 1),
        new("docs/conventions/UI_STANDARDS.md", "date", 5),
        new("docs/plans/DIALOGS.md", "count", 5),
        new("docs/plans/DIALOGS.md", "date", 3),
        new("docs/plans/ROADMAP.md", "date", 2),
    ];

    [Fact]
    public void HoldEverySnapshotAtItsRecordedCount()
    {
        var documents = HandwrittenDocuments.All();
        documents.Should().HaveCountGreaterThan(30, "a rule that reads a handful of documents checks little");

        var measured = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            var prose = document.Lines.Where(line => line.IsProse).Select(line => Prose(line.Text)).ToList();
            foreach (var (kind, patterns) in new[] { (Date, Dates), (Count, Counts) })
            {
                var found = prose.Sum(text => patterns.Sum(pattern => pattern.Matches(text).Count));
                if (found > 0)
                {
                    measured[Key(document.Path, kind)] = found;
                }
            }
        }

        var problems = HandwrittenDocuments.Disagreements(
            Register.ToDictionary(entry => Key(entry.File, entry.Kind), entry => entry.Count, StringComparer.Ordinal),
            measured,
            (key, count) => $"new(\"{key.Replace(" :: ", "\", \"", StringComparison.Ordinal)}\", {count}),");

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a document states the rule and the reason, not the day or the size of the tree: leave the date " +
            "to git log and the count to the code or docs/STATUS.md, and lower the record the moment a " +
            "document loses one");
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

    private static string Key(string file, string kind) => $"{file} :: {kind}";
}
