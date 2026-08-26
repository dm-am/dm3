using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A refusal is read by the person who was refused, and this interface is Russian.
/// </summary>
/// <remarks>
/// The message of an HttpException is not a developer note. The middleware puts it
/// into the title of the problem document and the client shows the title as it
/// stands, so a blog owner who blacklisted the same reader twice was answered
/// "User 'vasya' is already blacklisted": English copy on a Russian screen, written
/// by a layer that never sees a screen. API_DESIGN keeps no machine-readable error
/// code precisely because this string is the message, which makes its language part
/// of the contract rather than a detail of the throw.
///
/// The check is textual, and for the same reason as the one in
/// ErrorBodyOwnershipShould: the offending value is a string argument, which no
/// type can constrain, and it is the shape of the neighbouring throw that the next
/// one gets copied from. Only sources under src/ are read: the helpers that raise
/// these exceptions inside the test suites answer nobody.
///
/// Two roads lead to the same reader: a thrown refusal, whose message becomes the
/// title of the problem document, and a failed validation, whose message becomes
/// one of its errors. Both are scanned for language; only the first is scanned for
/// repetition, because a validator saying "введите почту" wherever there is an
/// email field is one sentence in its places, not one refusal spelled twice.
///
/// The wording itself is not checked: only that it is written in the language of
/// the interface, that it spells the letter "e" without the dots, and — for
/// refusals — that a wording written twice was not written twice. A message taken
/// from a shared constant leaves no literal at the site and is invisible to all
/// three, which is the intended outcome and not a hole.
/// </remarks>
public class RefusalCopyShould
{
    /// <summary>
    /// Where a refusal takes its wording: the two exceptions whose message the
    /// middleware turns into a title, and the factory call itself.
    /// </summary>
    /// <remarks>
    /// The factory is read because not every refusal can be thrown. The rate
    /// limiter answers from OnRejected, outside MVC, with no request left to
    /// throw into, so it builds the document itself — and its title stayed
    /// English for as long as this scan looked at throw sites alone. A call that
    /// passes a constant holds no literal and is invisible here, which is the
    /// intended outcome.
    /// </remarks>
    private static readonly Regex RefusalWording = new(
        @"new\s+(?:HttpException|HttpBadRequestException)\s*\(" +
        @"|Create(?:Validation)?ProblemDetails\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Where a failed validation takes its wording.
    /// </summary>
    /// <remarks>
    /// A refusal by the other road: the message of a FluentValidation rule and of a
    /// data annotation goes into the errors of the problem document, and the client
    /// shows it verbatim. Written in English at 79 sites, it answered a Russian
    /// screen with "Title must be between 3 and 200 characters" — and the scan meant
    /// to prevent exactly that read only throw sites.
    ///
    /// Held apart from the throw sites because only the language rules apply to
    /// both. A wording repeated across validators is not the drift the deduplication
    /// rule is about: every DTO with an email field asks for an email the same way,
    /// and that is one sentence said in the places it belongs, not one refusal
    /// spelled twice.
    /// </remarks>
    private static readonly Regex ValidationWording = new(
        @"WithMessage\s*\(" +
        @"|ErrorMessage\s*=\s*" +
        // A rule that yields its own message instead of handing one to the
        // library. The schema rules do it that way, and the five sentences they
        // return stayed English through a translation pass that read call sites
        // alone — the reader gets them the same way as any other validation error.
        @"|yield\s+return\s+(?=\$?"")",
        RegexOptions.Compiled);

    /// <summary>
    /// A FluentValidation placeholder, which is filled in by the library rather
    /// than read by anyone: {PropertyName}, {{{ErrorMessage}}} and their kin.
    /// </summary>
    private static readonly Regex PlaceholderOnly = new(
        @"^\s*(?:\{+\w+\}+\s*)+$", RegexOptions.Compiled);

    /// <summary>
    /// Russian letters, matched by Unicode block. A hand-written a-to-ya range would
    /// leave out the letter at U+0451, and that letter is named by number here rather
    /// than typed: a file that keeps a character out of messages should not carry it.
    /// </summary>
    private static readonly Regex Cyrillic = new(@"\p{IsCyrillic}", RegexOptions.Compiled);

    /// <summary>That same letter and its capital, by number and for the same reason.</summary>
    private static readonly char[] EWithDots = [(char)0x0451, (char)0x0401];

    /// <summary>
    /// The other noun for the thing the chat calls an event, by number for the same
    /// reason the letter above is spelled that way: a file that keeps a word out of
    /// messages should not carry it. The stem covers every case ending at once, and
    /// it is looked for in refusals alone — a security log line is an event in the
    /// ordinary sense of the word and says so.
    /// </summary>
    private static readonly Regex OtherNounForAnEvent = new(
        @"[Сс]обыти", RegexOptions.Compiled);

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
    public void CallTheChatEventByTheNameTheInterfaceGivesIt()
    {
        var offenders = EveryWording()
            .Where(site => site.Messages.Any(message => OtherNounForAnEvent.IsMatch(message)))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty(
            "the strip over the chat and the hint over the composer name it with the " +
            "borrowed word, and this refusal is read on that same screen a second " +
            "later: one thing answering to two nouns is two things to the reader");
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

    /// <summary>
    /// The wording of a message with the values it interpolates taken out: the same
    /// refusal phrased over {commentId} in one service and over {id} in the next is
    /// one wording written twice, not two wordings.
    /// </summary>
    private static string Normalize(string message) =>
        Whitespace.Replace(Hole.Replace(message, "{}"), " ").Trim();

    private static IReadOnlyList<ThrowSite> ThrowSites() =>
        Sites(RefusalWording, atLeast: 200);

    /// <summary>Every wording a refused person reads, of either kind.</summary>
    private static IReadOnlyList<ThrowSite> EveryWording() =>
        [.. ThrowSites(), .. Sites(ValidationWording, atLeast: 100)];

    private static IReadOnlyList<ThrowSite> Sites(Regex wording, int atLeast)
    {
        var root = RepositoryRoot;
        var sites = new List<ThrowSite>();

        foreach (var source in RepositoryFiles.CsharpFiles(Path.Combine(root.FullName, "src"), BuildOutput))
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

        sites.Should().HaveCountGreaterThanOrEqualTo(atLeast,
            "a rule that matches nothing passes");
        return sites;
    }

    /// <summary>
    /// The literals of one constructor call that a person could read: everything up
    /// to the parenthesis matching the opening one, minus the literals standing in a
    /// dictionary key position. ["password"] names the field the error is filed
    /// under, not the error, and it is written in English on purpose.
    /// </summary>
    /// <remarks>
    /// A call holding no literal at all takes its message from a constant or from a
    /// variable, and this scan says nothing about it: the dictionary is the outcome
    /// the rule wants, so a throw that reads from it has nothing left to check here.
    /// </remarks>
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
    /// Reads the literal opening at this quote and returns its text together with
    /// the index past its closing quote. Raw and verbatim literals are read on their
    /// own terms: in both of them a quote ends the string where a regular scan would
    /// see an escape, and getting that wrong swallows the rest of the file.
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

    private static DirectoryInfo RepositoryRoot => DM.Testing.RepositoryLayout.RootDirectory;

    private sealed record ThrowSite(string Source, int Line, IReadOnlyList<string> Messages);
}
