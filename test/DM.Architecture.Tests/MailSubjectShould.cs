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
/// Every letter the site sends opens its subject with the same prefix, and that
/// prefix is the only place the site is named.
/// </summary>
/// <remarks>
/// Nine subjects were written in five shapes: four appended the site name after a
/// dash, four buried it inside the phrase, one prepended it, and one of the four
/// opened with a warning sign, the only emoji in the product. A mailbox filter is
/// written against the beginning of a subject, so a reader who filtered on the
/// site name kept all forty-eight notifications and lost every security letter,
/// including the warning that the address on the account is being changed.
///
/// The subject is a literal inside the sender, so this rule reads sources rather
/// than assemblies: nothing builds these letters at test time and no test sends
/// mail. Only files that construct an EmailLetter are read, which keeps the
/// moderation ticket subject and the queue message property out of the match, and
/// comment lines are skipped so that quoting a bad subject in prose does not fail
/// the build.
/// </remarks>
public class MailSubjectShould
{
    private const string Prefix = "Dungeon Master: ";
    private const string SiteName = "Dungeon Master";
    private const string LetterType = "EmailLetter";

    /// <summary>
    /// A subject written as a literal: assigned to the Subject property of a
    /// letter, or prepared in a local named subject. A subject computed elsewhere
    /// is out of reach of a source scan and is not matched.
    /// </summary>
    private static readonly Regex SubjectLiteral = new(
        @"(?:^|[^\w.])[Ss]ubject\s*=\s*\$?""((?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled);

    private static readonly string[] SkippedDirectories = ["obj", "bin", "node_modules", "coverage", "dist"];

    [Fact]
    public void OpenWithTheSiteName()
    {
        var subjects = Subjects();

        subjects.Should().HaveCountGreaterOrEqualTo(5,
            "a rule that matches nothing passes: the account senders alone write more subjects than that");

        var offenders = subjects
            .Where(subject => !subject.Text.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "a mailbox filter is written against the start of the subject, so the site name goes there and nowhere else");
    }

    [Fact]
    public void NameTheSiteOnce()
    {
        var offenders = Subjects()
            .Where(subject => subject.Text.StartsWith(Prefix, StringComparison.Ordinal))
            .Where(subject => subject.Text[Prefix.Length..].Contains(SiteName, StringComparison.Ordinal))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "the prefix has already named the site, and a second mention spends the width the subject has in a list");
    }

    [Fact]
    public void CarryNoEmoji()
    {
        var offenders = Subjects()
            .Where(subject => subject.Text.Any(IsPictorial))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "one letter out of nine drew a sign the other eight did not, which reads as a different sender rather than as urgency");
    }

    [Fact]
    public void CallTheChannelInRussian()
    {
        var offenders = Subjects()
            .Where(subject => subject.Text.Contains("email", StringComparison.OrdinalIgnoreCase))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "the interface names this channel in Russian, and the subject of a letter is interface text like any other");
    }

    private static bool IsPictorial(char symbol) =>
        char.IsSurrogate(symbol)
        || CharUnicodeInfo.GetUnicodeCategory(symbol) == UnicodeCategory.OtherSymbol;

    private static string Describe(MailSubject subject) =>
        subject.Source + ":" + subject.Line + " -> " + subject.Text;

    private static IReadOnlyList<MailSubject> Subjects()
    {
        var root = RepositoryRoot;
        var subjects = new List<MailSubject>();

        foreach (var source in SourceFiles(Path.Combine(root, "src")))
        {
            var lines = File.ReadAllLines(source);
            if (!lines.Any(line => line.Contains(LetterType, StringComparison.Ordinal)))
            {
                continue;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                if (IsComment(lines[index]))
                {
                    continue;
                }

                foreach (Match match in SubjectLiteral.Matches(lines[index]))
                {
                    subjects.Add(new MailSubject(
                        Path.GetRelativePath(root, source).Replace('\\', '/'),
                        index + 1,
                        match.Groups[1].Value));
                }
            }
        }

        return subjects;
    }

    private static bool IsComment(string line)
    {
        var code = line.TrimStart();
        return code.StartsWith("//", StringComparison.Ordinal)
            || code.StartsWith("*", StringComparison.Ordinal)
            || code.StartsWith("/*", StringComparison.Ordinal);
    }

    /// <summary>
    /// Enumerates the C# sources under a directory, skipping build output and the
    /// client package tree: they hold no senders and are large enough to matter.
    /// </summary>
    private static IEnumerable<string> SourceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            yield return file;
        }

        foreach (var nested in Directory.EnumerateDirectories(directory))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(nested)))
            {
                continue;
            }

            foreach (var file in SourceFiles(nested))
            {
                yield return file;
            }
        }
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private sealed record MailSubject(string Source, int Line, string Text);
}
