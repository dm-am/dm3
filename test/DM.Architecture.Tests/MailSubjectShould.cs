using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every letter the site sends opens its subject with the same prefix, and that
/// prefix is the only place the site is named.
/// </summary>
/// <remarks>
/// A mailbox filter matches the beginning of a subject. The prefix is written once, in MailSubject, and
/// AccountLetterShould sends every account letter to see it arrive. Sources only here: what a run does not
/// reach is a subject written past MailSubject, and the topics the prefix is put in front of. Only files
/// that construct a letter are read, comment lines skipped.
/// </remarks>
public class MailSubjectShould
{
    private const string SiteName = "Dungeon Master";

    /// <summary>
    /// What marks a source as one that sends a letter.
    /// </summary>
    /// <remarks>Constructing the letter, or deriving from <c>AccountMailSender</c>, which constructs it.</remarks>
    private static readonly string[] LetterTypes = ["EmailLetter", "AccountMailSender"];

    /// <summary>
    /// A subject that does not go through MailSubject: anything assigned to the Subject of a letter but
    /// MailSubject.Of, or the subject of another message carried over as it is.
    /// </summary>
    private static readonly Regex SubjectPastThePrefix = new(
        @"(?:^|[^\w.])[Ss]ubject\s*=(?>\s*)(?!MailSubject\.Of\(|\w+\.Subject\b)",
        RegexOptions.Compiled);

    /// <summary>
    /// A topic written as a literal: handed to MailSubject.Of, or to Deliver after the address. A topic
    /// computed elsewhere is out of reach of a source scan and is not matched.
    /// </summary>
    private static readonly Regex TopicLiteral = new(
        @"(?:MailSubject\.Of\(|\bDeliver\(\s*\w+,\s*)\$?""((?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled);

    private static readonly string[] SkippedDirectories = ["obj", "bin", "node_modules", "coverage", "dist"];

    [Fact]
    public void PutThePrefixInFrontOfEverySubject()
    {
        var offenders = Lines()
            .Where(line => SubjectPastThePrefix.IsMatch(line.Text))
            .Select(line => line.Source + ":" + line.Number + " -> " + line.Text.Trim())
            .ToList();

        offenders.Should().BeEmpty(
            "a mailbox filter is written against the start of the subject, and MailSubject.Of is what puts the site name there");
    }

    [Fact]
    public void FindTheTopicsOfTheLetters() =>
        Topics().Should().HaveCountGreaterThanOrEqualTo(5,
            "a rule that matches nothing passes: the account senders alone write more topics than that");

    [Fact]
    public void NameTheSiteOnce()
    {
        var offenders = Topics()
            .Where(topic => topic.Text.Contains(SiteName, StringComparison.Ordinal))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "the prefix has already named the site, and a second mention spends the width the subject has in a list");
    }

    [Fact]
    public void CarryNoEmoji()
    {
        var offenders = Topics()
            .Where(topic => topic.Text.Any(IsPictorial))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "one letter out of nine drew a sign the other eight did not, which reads as a different sender rather than as urgency");
    }

    [Fact]
    public void CallTheChannelInRussian()
    {
        var offenders = Topics()
            .Where(topic => topic.Text.Contains("email", StringComparison.OrdinalIgnoreCase))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "the interface names this channel in Russian, and the subject of a letter is interface text like any other");
    }

    private static bool IsPictorial(char symbol) =>
        char.IsSurrogate(symbol)
        || CharUnicodeInfo.GetUnicodeCategory(symbol) == UnicodeCategory.OtherSymbol;

    private static string Describe(SourceLine topic) =>
        topic.Source + ":" + topic.Number + " -> " + topic.Text;

    private static IReadOnlyList<SourceLine> Topics() => Lines()
        .SelectMany(line => TopicLiteral.Matches(line.Text)
            .Select(match => line with { Text = match.Groups[1].Value }))
        .ToList();

    /// <summary>The code lines of every source that constructs a letter.</summary>
    private static IReadOnlyList<SourceLine> Lines()
    {
        var root = RepositoryLayout.Root;
        var found = new List<SourceLine>();

        foreach (var source in RepositoryFiles.Files(Path.Combine(root, "src"), "*.cs", SkippedDirectories))
        {
            var lines = File.ReadAllLines(source);
            if (!lines.Any(line => LetterTypes.Any(type =>
                    line.Contains(type, StringComparison.Ordinal))))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, source).Replace('\\', '/');
            found.AddRange(lines
                .Select((text, index) => new SourceLine(relative, index + 1, text))
                .Where(line => !IsComment(line.Text)));
        }

        return found;
    }

    private static bool IsComment(string line)
    {
        var code = line.TrimStart();
        return code.StartsWith("//", StringComparison.Ordinal)
            || code.StartsWith("*", StringComparison.Ordinal)
            || code.StartsWith("/*", StringComparison.Ordinal);
    }

    private sealed record SourceLine(string Source, int Number, string Text);
}
