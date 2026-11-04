using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A hand-written document carries none of the marks that read as machine text:
/// the semicolon in prose, the long and the short dash, the curly quotes and the
/// ellipsis typed as one character.
/// </summary>
/// <remarks>
/// CODE_STYLE sets the rules; the client counts its own dashes, U+0451 and guillemets are
/// InterfaceVocabularyShould's. A semicolon in code is code, the other marks count everywhere.
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

    [Fact]
    public void CarryNoneOfTheMarks()
    {
        var documents = HandwrittenDocuments.All();
        documents.Should().NotBeEmpty("a rule that reads no document checks nothing");
        var upstream = BlueprintSkills();

        var marked = documents
            .Where(document => !upstream.Any(skill => document.Path.StartsWith(skill, StringComparison.Ordinal)))
            .SelectMany(document => Count(document.Lines).Select(found => $"{document.Path} :: {found.Key}: {found.Value}"));

        string.Join(Environment.NewLine, marked).Should().BeEmpty(
            "write a comma, a colon, a full stop or straight quotes instead");
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

    /// <summary>The folders of the blueprint skills, kept byte for byte as upstream writes them; ours are not in the lock.</summary>
    private static string[] BlueprintSkills()
    {
        using var lockFile = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryLayout.Root, "skills-lock.json")));
        return [.. lockFile.RootElement.GetProperty("skills").EnumerateObject().Select(skill => $".claude/skills/{skill.Name}/")];
    }

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
}
