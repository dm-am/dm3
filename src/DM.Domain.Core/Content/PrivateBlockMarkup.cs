using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DM.Domain.Core.Content;

/// <summary>
/// The shape of the [private] block, in one place.
///
/// Two paths depend on spelling it the same way: the renderer, which has to
/// hand the parser a tag it recognises before the visibility filter can see a
/// node at all, and the post save path, which records who each block is for
/// under the key the visitor will later look it up by. A second copy of these
/// rules in the other assembly is how the two halves drift apart — and the last
/// time they did, every private line in the product's own syntax was served to
/// the whole room.
/// </summary>
public static class PrivateBlockMarkup
{
    /// <summary>Tag name of the private addressee block.</summary>
    public const string TagName = "private";

    /// <summary>
    /// A whole block, opening tag through closing tag, for the paths that have to
    /// cut one out of raw text: the stored search vector, the ILike filter behind
    /// it and the snippet the search results show.
    /// </summary>
    /// <remarks>
    /// One string because two of those paths are PostgreSQL and one is .NET, and
    /// the two engines read the same text differently. The optional attribute
    /// carries a lazy quantifier of its own because Postgres takes the greediness
    /// of a whole expression from its first quantifier: with a greedy one there,
    /// the lazy body below it was ignored and a single substitution ate everything
    /// from the first opening tag to the last closing one. Public text standing
    /// between two private blocks disappeared from the index, from the filter and
    /// from the snippet — and only in Postgres, so the .NET copy of the same
    /// string agreed with nothing.
    /// </remarks>
    public const string BlockPattern = @"\[private(=[^\]]*)??\][\s\S]*?\[/private\]";

    /// <summary>
    /// Whether every private block in the text is closed, and none is opened
    /// inside another.
    /// </summary>
    /// <remarks>
    /// An unclosed block is not a block: nothing cuts it out, so its text is
    /// indexed, matched and shown in a search preview, while the page renders it
    /// as private and hides it. The author sees a hidden line and the search sees
    /// a public one. A nested block is the other half of the same problem — the
    /// cut ends at the first closing tag, and the remainder of the outer block
    /// comes back out as public text.
    ///
    /// Refused at the save path rather than repaired: what the author meant by a
    /// tag they did not close cannot be guessed, and the two possible guesses —
    /// close it at the end, or drop it — differ by exactly who reads the rest of
    /// the post.
    ///
    /// A verbatim block decides whether the tags inside it are tags at all, so
    /// the count is taken over the text the parser will read as markup and not
    /// over the whole string — see <see cref="RegionsReadAsMarkup"/>.
    /// </remarks>
    /// <param name="source">Raw BBCode as the author submitted it</param>
    public static bool IsBalanced(string? source) => FindImbalance(source) is null;

    /// <summary>
    /// What to tell the author when <see cref="IsBalanced"/> refuses: which
    /// spelling was read, where it stands, and what closes it.
    /// </summary>
    /// <remarks>
    /// A refusal nobody can act on is indistinguishable from a broken form. The
    /// bare error code this used to carry named neither the tag nor its place,
    /// so the author of a long post was told that something in it was wrong and
    /// left to find it by bisection.
    /// </remarks>
    public static string DescribeBalanceRefusal(string? source) =>
        FindImbalance(source) ?? "Скрытый блок написан неправильно.";

    /// <summary>
    /// Whether the text carries hiding markup at all, wherever the parser would
    /// read it as markup.
    /// </summary>
    /// <remarks>
    /// The question a surface that does not declare the tag has to ask. There
    /// the tag is not markup, nothing hides anything, and the line the author
    /// meant for one reader is published with the tag still around it — so the
    /// save path refuses instead of publishing. The predicate lives here rather
    /// than beside the tag catalogue because the catalogue is infrastructure and
    /// a validator may not reach into it: a validator knows its own surface by
    /// being the validator of that surface, and asks this only about the text.
    ///
    /// Read broadly on purpose — the name followed by a bracket, an equals sign
    /// or a space is a tag by the grammar's own token rule, whether or not the
    /// attribute after it was ever terminated. Erring towards refusing is the
    /// safe direction here: a refused draft is rewritten, and a published secret
    /// is not taken back.
    /// </remarks>
    public static bool ContainsPrivateMarkup(string? source) => FindPrivateMarkup(source) is not null;

    /// <summary>
    /// What to tell the author on a surface that does not declare the tag: the
    /// spelling, its place, where the tag does work, and how to show it as an
    /// example instead.
    /// </summary>
    public static string DescribeSurfaceRefusal(string? source)
    {
        var found = FindPrivateMarkup(source);
        return found is null
            ? "Скрытая разметка здесь не работает."
            : $"Скрытая разметка здесь не работает: {SpellingAt(source!, found.Index)}, " +
              $"символ {found.Index + 1}. Ее текст увидят все читатели. " +
              "Скрытый блок есть только в игровом посте. Чтобы показать разметку примером, " +
              $"поставьте ее между [{VerbatimTagName}] и [/{VerbatimTagName}].";
    }

    /// <summary>
    /// The imbalance to report, or null when the text is well formed.
    /// </summary>
    private static string? FindImbalance(string? source)
    {
        if (string.IsNullOrEmpty(source) ||
            source.IndexOf(TagName, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        var regions = RegionsReadAsMarkup(source, out var unclosedVerbatim);

        // Hiding markup written after a verbatim block the author never closed.
        // The count agrees with itself there — the tags are all present, in
        // pairs — and the text still reaches the room, because the renderer
        // lifts a verbatim block out only when it finds the closing tag and the
        // parser refuses to read anything inside one it does not.
        if (unclosedVerbatim is { } opening)
        {
            var stranded = PrivateTokenRegex.Match(source, opening.Index);
            if (stranded.Success)
            {
                return $"Скрытая разметка внутри незакрытого блока [{opening.Name}]: " +
                       $"{SpellingAt(source, stranded.Index)}, символ {stranded.Index + 1}. " +
                       $"Закройте блок тегом [/{opening.Name}] или уберите скрытую разметку.";
            }
        }

        var depth = 0;
        Match? opened = null;
        foreach (var (match, isOpen) in TagsReadAsMarkup(source, regions))
        {
            if (isOpen)
            {
                if (depth == 1)
                {
                    return $"Скрытый блок внутри скрытого: {SpellingAt(source, match.Index)}, " +
                           $"символ {match.Index + 1}. Закройте первый тегом [/{TagName}] " +
                           "до начала второго.";
                }

                depth = 1;
                opened = match;
            }
            else
            {
                if (depth == 0)
                {
                    return $"Лишний закрывающий тег: {SpellingAt(source, match.Index)}, " +
                           $"символ {match.Index + 1}. Скрытый блок перед ним не открыт.";
                }

                depth = 0;
            }
        }

        return depth == 0
            ? null
            : $"Скрытый блок не закрыт: {SpellingAt(source, opened!.Index)}, " +
              $"символ {opened.Index + 1}. Закройте его тегом [/{TagName}] или уберите.";
    }

    /// <summary>
    /// The first piece of hiding markup the parser would read as markup, or
    /// null when there is none.
    /// </summary>
    private static Match? FindPrivateMarkup(string? source)
    {
        if (string.IsNullOrEmpty(source) ||
            source.IndexOf(TagName, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        var regions = RegionsReadAsMarkup(source, out var unclosedVerbatim);

        // The tail of an unclosed verbatim block is not an example of anything:
        // the block carries no closing tag, so it carries no instruction to show
        // its content literally, and the doubt is resolved towards refusing.
        var tail = unclosedVerbatim?.Index ?? int.MaxValue;

        // One pass over the tags against one pass over the spans, both in
        // document order: restarting the scan inside every span costs a scan of
        // the whole remainder per verbatim block, which a body built out of them
        // turns into a quadratic walk.
        var regionIndex = 0;
        foreach (Match match in PrivateTokenRegex.Matches(source))
        {
            if (match.Index >= tail)
            {
                return match;
            }

            while (regionIndex < regions.Count && regions[regionIndex].End <= match.Index)
            {
                regionIndex++;
            }

            if (regionIndex >= regions.Count)
            {
                return null;
            }

            if (match.Index >= regions[regionIndex].Start)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Where a verbatim block opens, and under which name.</summary>
    private readonly record struct VerbatimOpening(int Index, string Name);

    /// <summary>
    /// The spans of the text the parser will read as markup: everything outside
    /// a verbatim block that the author closed.
    /// </summary>
    /// <remarks>
    /// Mirrors what the renderer does before the parse, tag for tag, because the
    /// two answers have to be the same one. A closed [code] or [noparse] block
    /// is lifted out whole and put back verbatim, so nothing inside it is ever a
    /// node and nothing inside it can hide anything: counting a [private] there
    /// refused the only way the product has of teaching its own syntax. An
    /// unclosed one is not lifted out at all, which is the opposite case and is
    /// reported through <paramref name="unclosedVerbatim"/>.
    ///
    /// The earliest opening wins and runs to its own closing tag, which is how
    /// the renderer's alternation reads the same text. Anything else would make
    /// this pass disagree with the pass it exists to predict.
    /// </remarks>
    private static List<(int Start, int End)> RegionsReadAsMarkup(
        string source, out VerbatimOpening? unclosedVerbatim)
    {
        var regions = new List<(int Start, int End)>();
        unclosedVerbatim = null;

        var position = 0;
        while (true)
        {
            var opening = VerbatimOpenRegex.Match(source, position);
            if (!opening.Success)
            {
                regions.Add((position, source.Length));
                return regions;
            }

            regions.Add((position, opening.Index));

            var name = opening.Groups["name"].Value.ToLowerInvariant();
            var closing = $"[/{name}]";
            var closingAt = source.IndexOf(closing, opening.Index + opening.Length,
                StringComparison.OrdinalIgnoreCase);
            if (closingAt < 0)
            {
                unclosedVerbatim = new VerbatimOpening(opening.Index, name);
                return regions;
            }

            position = closingAt + closing.Length;
        }
    }

    /// <summary>
    /// Every opening and closing tag inside the given spans, in document order.
    /// </summary>
    /// <remarks>
    /// One pass over the tags against one pass over the spans, both already in
    /// document order. Restarting the tag scan inside every span costs a scan of
    /// the whole remainder per verbatim block, which a body built out of them
    /// turns into a quadratic walk.
    /// </remarks>
    private static IEnumerable<(Match Match, bool IsOpen)> TagsReadAsMarkup(
        string source, IReadOnlyList<(int Start, int End)> regions)
    {
        var tags = OpenTagRegex.Matches(source).Select(match => (Match: match, IsOpen: true))
            .Concat(CloseTagRegex.Matches(source).Select(match => (Match: match, IsOpen: false)))
            .OrderBy(tag => tag.Match.Index);

        var regionIndex = 0;
        foreach (var tag in tags)
        {
            while (regionIndex < regions.Count && regions[regionIndex].End <= tag.Match.Index)
            {
                regionIndex++;
            }

            if (regionIndex >= regions.Count)
            {
                yield break;
            }

            if (tag.Match.Index >= regions[regionIndex].Start)
            {
                yield return tag;
            }
        }
    }

    /// <summary>
    /// The tag as the author wrote it, for quoting back in a refusal: from the
    /// opening bracket to the closing one, bounded, on a single line.
    /// </summary>
    /// <remarks>
    /// Bounded because an attribute value has no length of its own, and put on
    /// one line because the message is shown as one. What is quoted is the tag,
    /// never the block's content: the point of the refusal is that the content
    /// has not been published yet.
    /// </remarks>
    private static string SpellingAt(string source, int index)
    {
        var limit = Math.Min(source.Length, index + MaxSpellingLength);
        var closing = source.IndexOf(']', index);
        var spelling = closing >= 0 && closing < limit
            ? source[index..(closing + 1)]
            : source[index..limit] + "...";
        return spelling.Replace('\r', ' ').Replace('\n', ' ');
    }

    /// <summary>How much of a tag a refusal quotes back.</summary>
    private const int MaxSpellingLength = 40;

    /// <summary>
    /// The verbatim block a refusal names as the way to write the markup
    /// literally. [code] does the same, but reads as source code.
    /// </summary>
    private const string VerbatimTagName = "noparse";

    /// <summary>
    /// Opening tag of a block whose content is shown as written. Both names,
    /// neither with an attribute, exactly as the renderer matches them.
    /// </summary>
    private static readonly Regex VerbatimOpenRegex = new(
        @"\[(?<name>code|noparse)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Either half of the hiding tag, read as a token: the name counts only
    /// when a bracket, an equals sign, a space or the end of the text follows
    /// it, so a word in prose does not become a tag.
    /// </summary>
    /// <remarks>
    /// Broader than <see cref="OpenTagRegex"/> on purpose, and used only where
    /// the question is whether hiding markup is present rather than how the
    /// blocks pair up. A spelling whose attribute was never terminated is still
    /// an author trying to hide a line, and on a surface that cannot hide one
    /// that is exactly what has to be refused.
    /// </remarks>
    private static readonly Regex PrivateTokenRegex = new(
        $@"\[/?{TagName}(?=[\]=\s]|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Opening tag in any casing, with or without an attribute. The quoted
    /// alternative is tried first so a value keeps its commas and spaces; the
    /// unquoted one runs to the closing bracket. Both quantifiers are bounded
    /// and neither nests, so the pattern stays linear in the content length.
    /// </summary>
    /// <remarks>
    /// Both value classes stop at an opening bracket, and that is what keeps
    /// the pass linear rather than quadratic: a bracket starts the next tag, so
    /// an attribute value that has run into one is an attribute that was never
    /// closed, and scanning past it only repeats the same failure from every
    /// following position. On a post built out of unclosed <c>[private=</c> the
    /// unbounded version scanned the whole remainder each time.
    ///
    /// A value that does contain a bracket is therefore not matched here at
    /// all, which used to mean it was not normalised and not parsed either -
    /// the leak this class exists to prevent. <see cref="StrayOpenTagRegex"/>
    /// closes that: whatever this pattern does not turn into a canonical
    /// opening tag is turned into an addressee-less one.
    /// </remarks>
    private static readonly Regex OpenTagRegex = new(
        @"\[private(?:=(?<value>""[^""\[]*""|[^\]\[]*))?\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// An opening tag that carries an attribute the parser will not read.
    /// </summary>
    /// <remarks>
    /// The lookahead is the parser's own rule for a quoted attribute value:
    /// anything but a quote or an opening bracket, plus a quote that is not the
    /// one before the closing bracket. What fails it is what the parser will
    /// also refuse — an unterminated attribute, or one carrying a bracket —
    /// and every one of those spellings used to reach the reader as ordinary
    /// text carrying the private line, because the parser saw no tag and the
    /// visibility filter therefore had no node to remove.
    ///
    /// Rewriting the opening tag to its attribute-less form is what makes
    /// <see cref="Normalise"/> total. The block keeps its content and loses its
    /// addressee, so it is shown to the author and the game leads and to nobody
    /// else. Erring towards a block nobody can read is deliberate: a private
    /// line withheld from the player it was meant for gets reported, and the
    /// same line handed to the room cannot be taken back.
    /// </remarks>
    private static readonly Regex StrayOpenTagRegex = new(
        @"\[private=(?!""(?>(?:[^""\[]|""(?!\]))*)""\])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Closing tag in any casing.</summary>
    private static readonly Regex CloseTagRegex = new(
        @"\[/private\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Bring every spelling of the tag to the one the parser's tag set
    /// recognises: lower-case name, attribute value in quotes.
    /// </summary>
    /// <remarks>
    /// The underlying parser matches the tag name case-sensitively and its
    /// attribute only when the value is quoted, so of the four spellings a user
    /// can produce only <c>[private="Name"]</c> was ever parsed as a tag. The
    /// other three — including <c>[private=Name]</c>, which is the only form the
    /// editor writes and the only one its help text teaches — were left as plain
    /// text, which means the visitor never saw a node to filter and the private
    /// line was served to every reader of the room.
    ///
    /// Normalising on the render path rather than at the editor is deliberate: a
    /// privacy filter has to hold for whatever reaches it. Any client, any
    /// hand-typed source and anything already stored is covered by one pass, and
    /// nothing downstream has to be trusted to spell the tag a particular way.
    ///
    /// A [noparse] block is not affected on the render path, because the caller
    /// lifts every verbatim block out of the text before running this and puts it
    /// back afterwards. It is affected anywhere this runs on its own, and there
    /// the tag is displayed with quotes the author did not type. Nothing leaks
    /// either way — noparse content is text by the author's own instruction — and
    /// the alternative is teaching this pass to parse block structure, which is
    /// the job of the parser it runs before.
    ///
    /// The postcondition is total, and that is the point: after this runs, no
    /// <c>[private=</c> is left in the text that the parser will not read as an
    /// opening tag. The second pass is what makes it total — see
    /// <see cref="StrayOpenTagRegex"/>. A block it has to downgrade keeps its
    /// content and loses its addressee, so it is shown to the author and to the
    /// game leads and to nobody else. That is the safe direction for a failure
    /// nobody can see: a private line withheld from the player it was meant for
    /// is a complaint, and the same line handed to the room is not recoverable.
    /// </remarks>
    public static string Normalise(string input)
    {
        if (string.IsNullOrEmpty(input) ||
            input.IndexOf(TagName, StringComparison.OrdinalIgnoreCase) < 0)
            return input;

        var normalised = OpenTagRegex.Replace(input, match => match.Groups["value"].Success
            ? $"[{TagName}=\"{ReadAttributeValue(match)}\"]"
            : $"[{TagName}]");

        normalised = StrayOpenTagRegex.Replace(normalised, $"[{TagName}]");

        return CloseTagRegex.Replace(normalised, $"[/{TagName}]");
    }

    /// <summary>
    /// Attribute value of every [private=...] block in the source, in document
    /// order, without repeats, spelled exactly the way the parser will report it
    /// to the visibility filter. Blocks without an attribute address nobody and
    /// are skipped.
    /// </summary>
    /// <remarks>
    /// Ordinal distinctness on purpose: the visitor looks the value up in an
    /// ordinal dictionary, so two spellings that differ only in case are two
    /// different blocks here as well.
    ///
    /// Occurrences inside [noparse] are collected too — this scans the source
    /// rather than the tree. That over-collects and never under-collects: an
    /// entry the parser never produces a node for is looked up by nobody.
    /// </remarks>
    public static IReadOnlyList<string> ReadAddresseeAttributes(string? source)
    {
        if (string.IsNullOrEmpty(source) ||
            source.IndexOf(TagName, StringComparison.OrdinalIgnoreCase) < 0)
            return Array.Empty<string>();

        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in OpenTagRegex.Matches(source))
        {
            if (!match.Groups["value"].Success) continue;
            var value = ReadAttributeValue(match);
            if (value.Length > 0 && seen.Add(value)) found.Add(value);
        }

        return found;
    }

    /// <summary>
    /// The attribute value as the parser will report it: a matched pair of
    /// surrounding quotes removed, everything inside them kept verbatim.
    /// </summary>
    /// <remarks>
    /// Any other quote is dropped rather than escaped — an addressee name never
    /// legitimately contains one, and a quote left in place closes the attribute
    /// early and produces a tag the parser rejects again. That matters most for
    /// the one-sided <c>[private="Name]</c>: it used to be passed through
    /// untouched on the theory that a leading quote meant the value was already
    /// quoted, the parser then refused the unterminated attribute, and the block
    /// was served as plain text to every reader of the room — the same leak the
    /// unquoted form had, through the spelling nobody checked.
    /// </remarks>
    private static string ReadAttributeValue(Match match)
    {
        var value = match.Groups["value"].Value;
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value.Replace("\"", string.Empty);
    }
}
