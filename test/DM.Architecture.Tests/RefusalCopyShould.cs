using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A refusal is read by the person who was refused, and this interface is Russian.
/// </summary>
/// <remarks>
/// The message of an HttpException becomes the title the client shows, and a validation message one of
/// its errors; both are read for language, only refusals for repetition. Textual, over src/ only. A
/// message taken from a constant leaves no literal here, which is intended.
/// </remarks>
public class RefusalCopyShould
{
    /// <summary>
    /// Where a refusal takes its wording: the two exceptions whose message the
    /// middleware turns into a title, and the factory call itself.
    /// </summary>
    /// <remarks>The rate limiter builds its document in OnRejected, outside MVC, so the factory is read too.</remarks>
    private static readonly Regex RefusalWording = new(
        @"new\s+(?:HttpException|HttpBadRequestException)\s*\(" +
        @"|Create(?:Validation)?ProblemDetails\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Where a failed validation takes its wording.
    /// </summary>
    /// <remarks>
    /// FluentValidation and data annotation messages go into the errors the client shows verbatim. Only
    /// the language rules apply: one email sentence in many validators is not drift.
    /// </remarks>
    private static readonly Regex ValidationWording = new(
        @"WithMessage\s*\(" +
        // A constant of that name declares a placeholder, not a message.
        @"|(?<!\bstring\s+)ErrorMessage\s*=\s*" +
        // A rule yielding its own message, as the schema rules do.
        @"|yield\s+return\s+(?=\$?"")",
        RegexOptions.Compiled);

    /// <summary>
    /// A FluentValidation placeholder, which is filled in by the library rather
    /// than read by anyone: {PropertyName}, {{{ErrorMessage}}} and their kin, the
    /// name interpolated from a constant of another class included.
    /// </summary>
    private static readonly Regex PlaceholderOnly = new(
        @"^\s*(?:\{+[\w.]+\}+\s*)+$", RegexOptions.Compiled);

    /// <summary>
    /// Russian letters by Unicode block; U+0451 is named by number, not typed.
    /// </summary>
    private static readonly Regex Cyrillic = new(@"\p{IsCyrillic}", RegexOptions.Compiled);

    /// <summary>That same letter and its capital, by number and for the same reason.</summary>
    private static readonly char[] EWithDots = [(char)0x0451, (char)0x0401];

    /// <summary>An interpolation hole is not wording: {commentId} and {id} say nothing.</summary>
    private static readonly Regex Hole = new(@"\{[^{}]*\}", RegexOptions.Compiled);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private static readonly string[] BuildOutput = ["bin", "obj", "node_modules", "coverage", "dist"];

    [Fact]
    public void BeWrittenInRussian()
    {
        var offenders = EveryWording()
            .Where(site => site.Messages.Count > 0)
            .Where(site => !site.Messages.Any(message => Cyrillic.IsMatch(message)))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "the middleware puts this message into the title and the client shows the title, " +
            "so it is interface copy and is written in the language of the interface");
    }

    [Fact]
    public void SpellEWithoutDots()
    {
        var offenders = EveryWording()
            .Where(site => site.Messages.Any(message => message.IndexOfAny(EWithDots) >= 0))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "CODE_STYLE spells this letter without the dots everywhere a person reads it, " +
            "and a refusal is read by a person");
    }

    [Fact]
    public void SayEachRefusalInOnePlace()
    {
        var repeated = ThrowSites()
            .SelectMany(site => site.Messages
                .Where(message => Cyrillic.IsMatch(message))
                .Select(message => (Wording: Normalize(message), site.Source, site.Line)))
            .GroupBy(written => written.Wording, StringComparer.Ordinal)
            .Where(wording => wording.Count() > 1)
            .Select(wording => wording.Key + " -> " +
                string.Join(", ", wording.Select(written => written.Source + ":" + written.Line)))
            .ToList();

        repeated.Should().BeEmpty(
            "the threshold is two because there is no larger count at which a copy turns safe: " +
            "the shared dictionary exists so that one refusal has one wording, and the second " +
            "literal copy is the place where the two of them start to drift apart");
    }

    private static string Describe(ThrowSite site) =>
        site.Source + ":" + site.Line + " -> " + string.Join(" | ", site.Messages);

    /// <summary>The wording with its interpolated values taken out: {commentId} and {id} are one wording.</summary>
    private static string Normalize(string message) =>
        Whitespace.Replace(Hole.Replace(message, "{}"), " ").Trim();

    private static IReadOnlyList<ThrowSite> ThrowSites() =>
        Sites(RefusalWording);

    /// <summary>Every wording a refused person reads, of either kind.</summary>
    private static IReadOnlyList<ThrowSite> EveryWording() =>
        [.. ThrowSites(), .. Sites(ValidationWording)];

    private static IReadOnlyList<ThrowSite> Sites(Regex wording)
    {
        var root = RepositoryLayout.RootDirectory;
        var sites = new List<ThrowSite>();

        foreach (var source in RepositoryFiles.Files(Path.Combine(root.FullName, "src"), "*.cs", BuildOutput))
        {
            var text = File.ReadAllText(source);
            var relative = Path.GetRelativePath(root.FullName, source).Replace('\\', '/');

            foreach (Match refusal in wording.Matches(text))
            {
                var line = text.Take(refusal.Index).Count(symbol => symbol == '\n') + 1;
                var messages = Messages(text, refusal.Index + refusal.Length)
                    .Where(message => !PlaceholderOnly.IsMatch(message))
                    .ToList();
                sites.Add(new ThrowSite(relative, line, messages));
            }
        }

        sites.Should().NotBeEmpty(
            "a rule that matches nothing passes");
        return sites;
    }

    /// <summary>
    /// The literals of one constructor call a person could read, minus dictionary keys: ["password"] names
    /// the field. A call with no literal reads its message from elsewhere and is not checked.
    /// </summary>
    private static IReadOnlyList<string> Messages(string text, int position)
    {
        var messages = new List<string>();
        var depth = 1;

        while (position < text.Length && depth > 0)
        {
            var symbol = text[position];
            if (symbol == '"')
            {
                var (value, end) = ReadLiteral(text, position);
                if (!IsDictionaryKey(text, end))
                {
                    messages.Add(value);
                }

                position = end;
                continue;
            }

            if (symbol == '(')
            {
                depth++;
            }
            else if (symbol == ')')
            {
                depth--;
            }

            position++;
        }

        return messages;
    }

    /// <summary>
    /// Reads the literal opening at this quote, and the index past its closing quote; raw and verbatim
    /// literals on their own terms.
    /// </summary>
    private static (string Value, int End) ReadLiteral(string text, int start)
    {
        if (start + 2 < text.Length && text[start + 1] == '"' && text[start + 2] == '"')
        {
            var fence = 0;
            while (start + fence < text.Length && text[start + fence] == '"')
            {
                fence++;
            }

            var closing = text.IndexOf(new string('"', fence), start + fence, StringComparison.Ordinal);
            return closing < 0
                ? (text[(start + fence)..], text.Length)
                : (text[(start + fence)..closing], closing + fence);
        }

        var verbatim = start > 0 && (text[start - 1] == '@' ||
            (text[start - 1] == '$' && start > 1 && text[start - 2] == '@'));
        var value = new StringBuilder();

        for (var position = start + 1; position < text.Length; position++)
        {
            var symbol = text[position];
            if (symbol == '"')
            {
                if (!verbatim || position + 1 >= text.Length || text[position + 1] != '"')
                {
                    return (value.ToString(), position + 1);
                }

                position++;
            }
            else if (symbol == '\\' && !verbatim && position + 1 < text.Length)
            {
                value.Append(symbol);
                position++;
            }

            value.Append(text[position]);
        }

        return (value.ToString(), text.Length);
    }

    private static bool IsDictionaryKey(string text, int afterLiteral)
    {
        while (afterLiteral < text.Length && char.IsWhiteSpace(text[afterLiteral]))
        {
            afterLiteral++;
        }

        return afterLiteral < text.Length && text[afterLiteral] == ']';
    }

    private sealed record ThrowSite(string Source, int Line, IReadOnlyList<string> Messages);
}
