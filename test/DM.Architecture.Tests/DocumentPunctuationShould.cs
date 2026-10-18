using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A hand-written document carries none of the marks that read as machine text:
/// the semicolon in prose, the long and the short dash, the curly quotes and the
/// ellipsis typed as one character.
/// </summary>
/// <remarks>
/// CODE_STYLE sets the rules for Russian text and the owner's memory repeats them
/// for every document: straight quotes, no semicolons, dashes or ellipses. The
/// client counts its own long dashes (EM_DASH_BUDGET of copy-rules.spec.ts), and
/// CODE_STYLE asks for the documentation to be counted by a budget of its own,
/// since one number over both sets would leave neither checkable. This is that
/// budget. The letter at U+0451 and the guillemets are held by
/// InterfaceVocabularyShould over the same documents and are not counted twice.
///
/// A semicolon inside a block of code or a code span is code, and code is outside
/// the rule. The other marks are counted wherever they stand: a dash in a comment
/// of an example is still a dash a person reads.
///
/// The documents carried hundreds of these marks on the day the gate arrived, so
/// each document is recorded at its count per mark (see
/// <see cref="HandwrittenDocuments"/> for the contract of the register). The three
/// places CODE_STYLE leaves to the long dash by grammar are in the counts too: the
/// register is where a dash that stays is visible, as EM_DASH_BUDGET is on the
/// client.
/// </remarks>
public class DocumentPunctuationShould
{
    private const string Semicolon = "semicolon";
    private const string EmDash = "em dash";
    private const string EnDash = "en dash";
    private const string CurlyQuote = "curly quote";
    private const string Ellipsis = "ellipsis";

    /// <summary>The marks counted wherever they stand, by number.</summary>
    private static readonly (string Mark, char[] Characters)[] Typographic =
    [
        (EmDash, [(char)0x2014]),
        (EnDash, [(char)0x2013]),
        (CurlyQuote, [(char)0x2018, (char)0x2019, (char)0x201C, (char)0x201D, (char)0x201E]),
        (Ellipsis, [(char)0x2026]),
    ];

    /// <summary>A document holding marks, at its count of one mark.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Mark">Which mark is counted.</param>
    /// <param name="Count">How many the document holds.</param>
    private sealed record Recorded(string File, string Mark, int Count);

    private static readonly Recorded[] Register =
    [
        new(".claude/CLAUDE.md", "em dash", 20),
        new(".claude/CLAUDE.md", "semicolon", 7),
        new(".claude/agents/code-reviewer.md", "em dash", 7),
        new(".claude/agents/code-reviewer.md", "semicolon", 8),
        new(".claude/agents/debugger.md", "em dash", 9),
        new(".claude/agents/frontend-developer.md", "em dash", 4),
        new(".claude/agents/frontend-developer.md", "semicolon", 3),
        new(".claude/skills/architecture/SKILL.md", "semicolon", 7),
        new(".claude/skills/codex-issue-coordinator/SKILL.md", "semicolon", 3),
        new(".claude/skills/html-doc/SKILL.md", "semicolon", 20),
        new(".claude/skills/review/SKILL.md", "en dash", 2),
        new("README.md", "em dash", 16),
        new("docs/PROGRESS.md", "ellipsis", 1),
        new("docs/PROGRESS.md", "em dash", 24),
        new("docs/PROGRESS.md", "semicolon", 32),
        new("docs/SURFACES.md", "ellipsis", 3),
        new("docs/SURFACES.md", "em dash", 115),
        new("docs/SURFACES.md", "semicolon", 52),
        new("docs/architecture/AUTHENTICATION.md", "em dash", 26),
        new("docs/architecture/AUTHENTICATION.md", "semicolon", 4),
        new("docs/architecture/AUTHORIZATION.md", "em dash", 57),
        new("docs/architecture/AUTHORIZATION.md", "semicolon", 14),
        new("docs/architecture/BBCODE_RENDERING.md", "em dash", 85),
        new("docs/architecture/BBCODE_RENDERING.md", "en dash", 4),
        new("docs/architecture/BBCODE_RENDERING.md", "semicolon", 28),
        new("docs/architecture/SYSTEM.md", "em dash", 24),
        new("docs/architecture/UPLOADS.md", "em dash", 12),
        new("docs/architecture/UPLOADS.md", "semicolon", 2),
        new("docs/conventions/API_DESIGN.md", "em dash", 71),
        new("docs/conventions/API_DESIGN.md", "semicolon", 17),
        new("docs/conventions/CODE_STYLE.md", "ellipsis", 1),
        new("docs/conventions/CODE_STYLE.md", "em dash", 53),
        new("docs/conventions/CODE_STYLE.md", "semicolon", 12),
        new("docs/conventions/DATA_STORAGE.md", "em dash", 43),
        new("docs/conventions/DATA_STORAGE.md", "semicolon", 5),
        new("docs/conventions/PATTERNS.md", "em dash", 115),
        new("docs/conventions/PATTERNS.md", "semicolon", 5),
        new("docs/conventions/PERFORMANCE.md", "em dash", 43),
        new("docs/conventions/PERFORMANCE.md", "semicolon", 4),
        new("docs/conventions/PROCESS.md", "em dash", 53),
        new("docs/conventions/PROCESS.md", "semicolon", 15),
        new("docs/conventions/SECURITY.md", "em dash", 31),
        new("docs/conventions/SECURITY.md", "semicolon", 8),
        new("docs/conventions/UI_STANDARDS.md", "ellipsis", 4),
        new("docs/conventions/UI_STANDARDS.md", "em dash", 54),
        new("docs/conventions/UI_STANDARDS.md", "semicolon", 90),
        new("docs/conventions/URL_STRUCTURE.md", "em dash", 7),
        new("docs/conventions/USERNAME_POLICY.md", "em dash", 3),
        new("docs/conventions/USERNAME_POLICY.md", "semicolon", 2),
        new("docs/guides/DEPLOYMENT.md", "em dash", 28),
        new("docs/guides/DEPLOYMENT.md", "semicolon", 5),
        new("docs/guides/LOCAL_SETUP.md", "em dash", 15),
        new("docs/guides/LOCAL_SETUP.md", "semicolon", 2),
        new("docs/guides/MONITORING.md", "em dash", 33),
        new("docs/guides/MONITORING.md", "semicolon", 6),
        new("docs/guides/POINT_OF_PRESENCE.md", "em dash", 5),
        new("docs/guides/TESTING.md", "em dash", 15),
        new("docs/guides/TESTING.md", "semicolon", 6),
        new("docs/plans/DIALOGS.md", "semicolon", 7),
        new("docs/plans/DM2_MIGRATION.md", "em dash", 7),
        new("docs/plans/DM2_MIGRATION.md", "semicolon", 5),
        new("docs/plans/ERROR_PAGES_AND_LORE.md", "em dash", 30),
        new("docs/plans/ROADMAP.md", "em dash", 2),
        new("docs/plans/ROADMAP.md", "semicolon", 4),
        new("docs/references/CONFIGURATION.md", "em dash", 8),
        new("docs/references/CONFIGURATION.md", "semicolon", 1),
        new("docs/references/GLOSSARY.md", "em dash", 38),
        new("docs/references/GLOSSARY.md", "semicolon", 4),
        new("scripts/visual/README.md", "semicolon", 4),
    ];

    [Fact]
    public void HoldEveryMarkAtItsRecordedCount()
    {
        var documents = HandwrittenDocuments.All();
        documents.Should().HaveCountGreaterThan(30, "a rule that reads a handful of documents checks little");

        var measured = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            foreach (var (mark, count) in Count(document.Lines))
            {
                measured[Key(document.Path, mark)] = count;
            }
        }

        var problems = HandwrittenDocuments.Disagreements(
            Register.ToDictionary(entry => Key(entry.File, entry.Mark), entry => entry.Count, StringComparer.Ordinal),
            measured,
            (key, count) => $"new(\"{key.Replace(" :: ", "\", \"", StringComparison.Ordinal)}\", {count}),");

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a document holds no more of these marks than the register records: write a comma, a colon, " +
            "a full stop or straight quotes instead, and lower the record the moment a document " +
            "loses one");
    }

    /// <summary>What is counted and what is code.</summary>
    [Theory]
    [InlineData("Одна фраза; вторая.", Semicolon, 1)]
    [InlineData("Код `a; b;` в строке.", Semicolon, 0)]
    [InlineData("```csharp\nvar a = 1;\n```", Semicolon, 0)]
    [InlineData("DM3 — платформа.", EmDash, 1)]
    [InlineData("```text\nгоды 2020–2026\n```", EnDash, 1)]
    [InlineData("“цитата”", CurlyQuote, 2)]
    [InlineData("и так далее…", Ellipsis, 1)]
    [InlineData("и так далее...", Ellipsis, 0)]
    public void CountAMarkTheWayTheRuleReadsIt(string text, string mark, int count) =>
        Count(HandwrittenDocuments.Lines(text)).GetValueOrDefault(mark).Should().Be(count);

    /// <summary>The count of every mark the lines hold, the marks they do not hold left out.</summary>
    private static Dictionary<string, int> Count(IEnumerable<HandwrittenDocuments.DocumentLine> lines)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (line.IsProse)
            {
                Add(counts, Semicolon, HandwrittenDocuments.WithoutCodeSpans(line.Text).Count(c => c == ';'));
            }

            foreach (var (mark, characters) in Typographic)
            {
                Add(counts, mark, line.Text.Count(c => Array.IndexOf(characters, c) >= 0));
            }
        }

        return counts;
    }

    private static void Add(Dictionary<string, int> counts, string mark, int count)
    {
        if (count > 0)
        {
            counts[mark] = counts.GetValueOrDefault(mark) + count;
        }
    }

    private static string Key(string file, string mark) => $"{file} :: {mark}";
}
