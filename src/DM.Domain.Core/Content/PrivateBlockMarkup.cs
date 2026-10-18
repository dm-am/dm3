using System;
using System.Collections.Generic;

namespace DM.Domain.Core.Content;

/// <summary>
/// The [private] block as the save path and the search read it.
/// </summary>
/// <remarks>
/// The renderer reads the block by the grammar of the parser (BBCodeParser.Reader);
/// a domain assembly references no parser, so the save path reads the same
/// rules here, for the one tag it has to answer about. The two are held to the
/// same rows of bbcodeGrammar.cases.ts by BbGrammarShould, and the rules are
/// written down once, in docs/architecture/BBCODE_RENDERING.md, section
/// "Скрытый блок [private]":
///
/// - every token <c>[private</c> followed by "]", "=", a whitespace character or
///   the end of the text is a hiding block;
/// - the block is well spelled only as <c>[private]</c> or <c>[private="..."]</c>,
///   whose value runs to the first quote followed by "]" and carries neither
///   "[" nor a line break; any other spelling is a block that addresses nobody;
/// - only <c>[/private]</c>, bare, closes it;
/// - a name is a name in any case, and only A-Z folds onto a-z (see AsciiEquals);
/// - inside a <c>[noparse]</c> closed by its own tag nothing is a tag; one that
///   was never closed is no block at all, and what follows it is read as it
///   would be without it. <c>[code]</c> is no such block: it changes the font
///   and nothing else, and a hiding block inside it hides.
///
/// Every scan here stops at the next opening bracket or jumps to a closing tag
/// found by a forward search whose answer is remembered, so each is linear in
/// the length of the text.
/// </remarks>
public static class PrivateBlockMarkup
{
    /// <summary>Tag name of the private addressee block.</summary>
    public const string TagName = "private";

    private const string ClosingTag = "[/private]";

    /// <summary>
    /// The blocks of the text for the paths that cut them out of a string rather
    /// than out of a tree: the stored search vector, the headline built from it
    /// and the preview. Used with <see cref="BlockPatternFlags"/> in PostgreSQL and
    /// with no options in .NET.
    /// </summary>
    /// <remarks>
    /// A second lock over text the tree walk has already cleaned, and a removal
    /// only. It reads the openings the renderer reads - the name in any case
    /// followed by "]", "=" or whitespace - and ends a block at the first
    /// <c>[/private]</c>, in any case as well, or at the end of the text when
    /// there is none. The name is spelled out letter by letter rather than read
    /// under a case-insensitive flag, because such a flag folds more than A-Z
    /// onto a-z: .NET reads the Kelvin sign as k under it.
    ///
    /// The body is written as "anything that is not the start of the closing
    /// tag" rather than as a lazy run up to it, so the expression means one thing
    /// in both engines: PostgreSQL takes the greediness of a whole expression from
    /// its first quantifier, and the two read an end anchor differently before a
    /// final line break.
    /// </remarks>
    public const string BlockPattern =
        @"\[[Pp][Rr][Ii][Vv][Aa][Tt][Ee](?=[\]=\s]|$)" +
        @"(?:[^\[]|\[(?!/[Pp][Rr][Ii][Vv][Aa][Tt][Ee]\]))*(?:\[/[Pp][Rr][Ii][Vv][Aa][Tt][Ee]\])?";

    /// <summary>The flags PostgreSQL applies <see cref="BlockPattern"/> with.</summary>
    public const string BlockPatternFlags = "g";

    /// <summary>
    /// Whether every private block in the text is well spelled and closed, and
    /// none is opened inside another or closes nothing.
    /// </summary>
    /// <remarks>
    /// An unclosed block runs to the end of the text on the page, and the author
    /// did not write that. A nested block is the other half of the same problem.
    /// A block the grammar did not accept addresses nobody on the page, and the
    /// author meant it for somebody. All of them are refused at the save path
    /// rather than repaired: what the author meant cannot be guessed, and the
    /// guesses differ by exactly who reads the rest of the post.
    /// </remarks>
    /// <param name="source">Raw BBCode as the author submitted it</param>
    public static bool IsBalanced(string? source) => FindImbalance(Read(source)) is null;

    /// <summary>
    /// What to tell the author when <see cref="IsBalanced"/> refuses: which
    /// spelling was read, where it stands, and how to write it.
    /// </summary>
    /// <remarks>
    /// A refusal nobody can act on is indistinguishable from a broken form. The
    /// bare error code this used to carry named neither the tag nor its place,
    /// so the author of a long post was told that something in it was wrong and
    /// left to find it by bisection.
    /// </remarks>
    public static string DescribeBalanceRefusal(string? source) =>
        FindImbalance(Read(source)) ?? "Скрытый блок написан неправильно.";

    /// <summary>
    /// Whether the text carries hiding markup at all, wherever the parser would
    /// read it as markup.
    /// </summary>
    /// <remarks>
    /// The question a surface that does not declare the tag has to ask. There
    /// the page erases such a block for every reader, the author included, and
    /// the line the author meant for one reader reaches nobody without a word
    /// said, so the save path refuses instead of publishing. The predicate
    /// lives here rather than beside the tag catalogue because the catalogue is infrastructure and
    /// a validator may not reach into it: a validator knows its own surface by
    /// being the validator of that surface, and asks this only about the text.
    ///
    /// Read broadly on purpose — a closing tag counts as well as an opening one,
    /// and so does a token after a [noparse] the author never closed. Erring
    /// towards refusing is the safe direction here: a refused draft is
    /// rewritten, and a published secret is not taken back.
    /// </remarks>
    public static bool ContainsPrivateMarkup(string? source) => FindPrivateMarkup(Read(source)) >= 0;

    /// <summary>
    /// What to tell the author on a surface that does not declare the tag: where
    /// the tag does work, the spelling, its place, and how to show it as an
    /// example instead.
    /// </summary>
    /// <remarks>
    /// The reason is where the tag works and nothing about who would read the
    /// block: a text that skipped this refusal has the block erased for every
    /// reader, and the refusal once told the author the opposite, that every
    /// reader would see it. The editor's markup check names the same case in
    /// the same words (bbcode.ts, "private-outside-game").
    /// </remarks>
    public static string DescribeSurfaceRefusal(string? source)
    {
        var text = Read(source);
        var found = FindPrivateMarkup(text);
        return found < 0
            ? SurfaceReason
            : $"{SurfaceReason}: {SpellingAt(text!, found)}, символ {found + 1}. " +
              "Чтобы показать разметку примером, " +
              $"поставьте ее между [{VerbatimTagName}] и [/{VerbatimTagName}].";
    }

    /// <summary>Why hiding markup is refused on a surface that does not declare it.</summary>
    private const string SurfaceReason = "Скрытый текст работает только в игровом тексте поста";

    /// <summary>
    /// Attribute value of every well spelled [private="..."] block of the text, in
    /// document order, without repeats, exactly as the parser reports it to the
    /// visibility filter. Blocks that name nobody are skipped.
    /// </summary>
    /// <remarks>
    /// Ordinal distinctness on purpose: the visitor looks the value up in an
    /// ordinal dictionary, so two spellings that differ only in case are two
    /// different blocks here as well. A block shown as an example inside a
    /// closed [noparse] is no block at all and is not collected; every block
    /// inside a [code], and after a [noparse] nobody closed, is, because the
    /// parser reads it as one.
    /// </remarks>
    public static IReadOnlyList<string> ReadAddresseeAttributes(string? source)
    {
        var found = new List<string>();
        var text = Read(source);
        if (!MayContainTag(text)) return found;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in Tokens(text!))
        {
            if (token.Kind is TokenKind.Opening &&
                token.Attribute is { Length: > 0 } value && seen.Add(value))
            {
                found.Add(value);
            }
        }

        return found;
    }

    /// <summary>
    /// Whether an attribute value names any addressee: a list of names separated
    /// by commas, the whitespace around each name not counted.
    /// </summary>
    /// <remarks>
    /// <c>[private=""]</c> and a value of commas and spaces alone name nobody, and
    /// such a block is the same block as a bare <c>[private]</c>.
    /// </remarks>
    public static bool NamesAddressees(string? attribute)
    {
        if (string.IsNullOrEmpty(attribute)) return false;

        foreach (var name in attribute.Split(','))
        {
            if (name.Trim().Length > 0) return true;
        }

        return false;
    }

    /// <summary>
    /// The text as the parser reads it, which is the text every question here is
    /// asked about: a character the parser removes before reading joins what
    /// stood around it, and the join can be a tag (see ParserInput). A place a
    /// refusal names is a place in this text.
    /// </summary>
    private static string? Read(string? source) =>
        string.IsNullOrEmpty(source) ? source : ParserInput.Of(source);

    /// <summary>
    /// The refusal to report, or null when the text is well formed.
    /// </summary>
    private static string? FindImbalance(string? source)
    {
        if (!MayContainTag(source)) return null;

        var text = source!;
        var opened = -1;
        var misspelledClosing = -1;
        var unclosedVerbatim = false;
        foreach (var token in Tokens(text))
        {
            switch (token.Kind)
            {
                // The parser reads the block after a [noparse] nobody closed as
                // a block, and the page hides it. The author almost always wrote
                // an example and forgot the closing tag, and without the refusal
                // that example would quietly become a secret, or its [/private]
                // would end a block early and show the rest. Refused to the end
                // of the text rather than to the end of the tag the [noparse]
                // stands in: no other tag is read here, so where that one ends
                // is not known, and refusing too much is the safe way to be
                // wrong. The editor's check reads the same rule.
                case TokenKind.UnclosedVerbatim:
                    unclosedVerbatim = true;
                    break;

                case TokenKind.Opening or TokenKind.Closing when unclosedVerbatim:
                    return $"Скрытая разметка после незакрытого [{VerbatimTagName}]: " +
                           $"{SpellingAt(text, token.Index)}, символ {token.Index + 1}. " +
                           $"Закройте [{VerbatimTagName}] тегом [/{VerbatimTagName}] " +
                           "или уберите скрытую разметку.";

                case TokenKind.Opening when token.Broken:
                    return $"Скрытый блок записан неправильно: {SpellingAt(text, token.Index)}, " +
                           $"символ {token.Index + 1}. Верная запись: [{TagName}=\"Имя\"], " +
                           $"имена через запятую, или [{TagName}] без имени.";

                case TokenKind.Opening when opened >= 0:
                    return $"Скрытый блок внутри скрытого: {SpellingAt(text, token.Index)}, " +
                           $"символ {token.Index + 1}. Закройте первый тегом [/{TagName}] " +
                           "до начала второго.";

                case TokenKind.Opening:
                    opened = token.Index;
                    break;

                case TokenKind.Closing when opened < 0:
                    return $"Лишний закрывающий тег: {SpellingAt(text, token.Index)}, " +
                           $"символ {token.Index + 1}. Скрытый блок перед ним не открыт.";

                case TokenKind.Closing:
                    opened = -1;
                    misspelledClosing = -1;
                    break;

                // The parser reads it as text, so the block runs on past it.
                // Named when the block ends unclosed: it is the closing tag the
                // author meant, and the place to fix.
                case TokenKind.BrokenClosing when opened >= 0 && misspelledClosing < 0:
                    misspelledClosing = token.Index;
                    break;
            }
        }

        if (opened < 0) return null;

        return misspelledClosing >= 0
            ? $"Закрывающий тег записан неправильно: {SpellingAt(text, misspelledClosing)}, " +
              $"символ {misspelledClosing + 1}. Скрытый блок закрывает только [/{TagName}]."
            : $"Скрытый блок не закрыт: {SpellingAt(text, opened)}, " +
              $"символ {opened + 1}. Закройте его тегом [/{TagName}] или уберите.";
    }

    /// <summary>
    /// Where the first piece of hiding markup stands, a closing tag of any
    /// spelling included, or -1 when there is none.
    /// </summary>
    private static int FindPrivateMarkup(string? source)
    {
        if (!MayContainTag(source)) return -1;

        foreach (var token in Tokens(source!))
        {
            if (token.Kind != TokenKind.UnclosedVerbatim) return token.Index;
        }

        return -1;
    }

    private static bool MayContainTag(string? source) =>
        !string.IsNullOrEmpty(source) && source.Contains(TagName, StringComparison.OrdinalIgnoreCase);

    private enum TokenKind
    {
        Opening,

        /// <summary>A closing tag the parser reads as one: [/private], in any case.</summary>
        Closing,

        /// <summary>
        /// Any other spelling of the closing tag, [/private="x"] or [/private and
        /// a space. The parser reads it as text, and it closes nothing.
        /// </summary>
        BrokenClosing,

        /// <summary>
        /// The opening of a [noparse] nobody closed. It is no block, and it hides
        /// nothing: reported so that a check can refuse the hiding markup written
        /// after it.
        /// </summary>
        UnclosedVerbatim
    }

    private readonly record struct Token(TokenKind Kind, int Index, bool Broken, string? Attribute);

    /// <summary>
    /// The tokens of the hiding tag, and the openings of the [noparse] blocks
    /// nobody closed, in document order. Nothing inside a closed [noparse] is a
    /// token.
    /// </summary>
    private static IEnumerable<Token> Tokens(string text)
    {
        var verbatimClosing = new VerbatimClosing(text);
        var position = 0;
        while (position < text.Length)
        {
            var bracket = text.IndexOf('[', position);
            if (bracket < 0) yield break;

            if (IsVerbatimOpeningAt(text, bracket))
            {
                var contentStart = bracket + VerbatimOpeningTag.Length;
                var closingAt = verbatimClosing.Find(contentStart);
                if (closingAt >= 0)
                {
                    position = closingAt + VerbatimClosingTag.Length;
                    continue;
                }

                // Not closed: no example of anything, and what follows is read
                // as it would be without it.
                yield return new Token(TokenKind.UnclosedVerbatim, bracket, false, null);
                position = contentStart;
                continue;
            }

            if (ReadOpening(text, bracket) is { } opening)
            {
                yield return opening.Token;
                position = opening.End;
                continue;
            }

            if (bracket + ClosingTag.Length <= text.Length && AsciiEquals(text, bracket, ClosingTag))
            {
                yield return new Token(TokenKind.Closing, bracket, false, null);
                position = bracket + ClosingTag.Length;
                continue;
            }

            if (IsClosingToken(text, bracket))
            {
                yield return new Token(TokenKind.BrokenClosing, bracket, true, null);
            }

            position = bracket + 1;
        }
    }

    /// <summary>Whether [noparse], in any case and with nothing after the name, opens at the index.</summary>
    private static bool IsVerbatimOpeningAt(string text, int bracket) =>
        bracket + VerbatimOpeningTag.Length <= text.Length && AsciiEquals(text, bracket, VerbatimOpeningTag);

    /// <summary>
    /// Where the closing tag of [noparse] stands, the last answer remembered.
    /// </summary>
    /// <remarks>
    /// Every opening of a [noparse] nobody closed asks again, from further on in
    /// the same text, and a text of such openings would otherwise be scanned to
    /// its end once per opening. An answer of "nowhere" stays true for every later
    /// start, and one that found the tag stays true for every start up to it.
    /// The parser remembers its answers the same way (Reader.FindClosing).
    /// </remarks>
    private sealed class VerbatimClosing(string text)
    {
        private (int From, int At)? known;

        internal int Find(int from)
        {
            if (known is { } answer && answer.From <= from && (answer.At < 0 || answer.At >= from))
            {
                return answer.At;
            }

            var at = -1;
            for (var scan = text.IndexOf('[', from); scan >= 0; scan = text.IndexOf('[', scan + 1))
            {
                if (scan + VerbatimClosingTag.Length <= text.Length && AsciiEquals(text, scan, VerbatimClosingTag))
                {
                    at = scan;
                    break;
                }
            }

            known = (from, at);
            return at;
        }
    }

    /// <summary>
    /// Whether the text at the index reads as the given lower-case spelling with
    /// A-Z folded onto a-z and nothing else folded. The caller makes sure the
    /// text is long enough.
    /// </summary>
    /// <remarks>
    /// Not one of the comparisons ignoring case the runtime offers: some of them
    /// fold more than A-Z (ToLowerInvariant and a regular expression ignoring case
    /// take the Kelvin sign for k), and the parser and the editor would then read
    /// one text two ways.
    /// </remarks>
    private static bool AsciiEquals(string text, int index, string lowerCase)
    {
        for (var offset = 0; offset < lowerCase.Length; offset++)
        {
            var character = text[index + offset];
            if (character is >= 'A' and <= 'Z') character = (char)(character + ('a' - 'A'));
            if (character != lowerCase[offset]) return false;
        }

        return true;
    }

    /// <summary>
    /// Where the name ends when the name starting at <paramref name="nameStart"/>
    /// makes a token of the hiding tag, or -1.
    /// </summary>
    private static int NameEnd(string text, int nameStart)
    {
        var nameEnd = nameStart;
        while (nameEnd < text.Length && IsNameLetter(text[nameEnd])) nameEnd++;

        return nameEnd - nameStart == TagName.Length &&
               AsciiEquals(text, nameStart, TagName) &&
               IsTokenEnd(text, nameEnd)
            ? nameEnd
            : -1;
    }

    /// <summary>A closing token of the hiding tag, "[/private" in any case, read broadly.</summary>
    private static bool IsClosingToken(string text, int bracket) =>
        bracket + 1 < text.Length && text[bracket + 1] == '/' && NameEnd(text, bracket + 2) > 0;

    /// <summary>
    /// The opening token of the hiding tag at the index, how it is spelled, and
    /// where it ends; null when there is none.
    /// </summary>
    private static (Token Token, int End)? ReadOpening(string text, int bracket)
    {
        var nameEnd = NameEnd(text, bracket + 1);
        if (nameEnd < 0) return null;

        if (nameEnd < text.Length)
        {
            if (text[nameEnd] == ']')
                return (new Token(TokenKind.Opening, bracket, false, string.Empty), nameEnd + 1);

            if (text[nameEnd] == '=' && ReadAttribute(text, nameEnd + 1) is { } attribute)
                return (new Token(TokenKind.Opening, bracket, false, attribute.Value), attribute.End);
        }

        return (new Token(TokenKind.Opening, bracket, true, null), BrokenEnd(text, nameEnd));
    }

    /// <summary>A quote, then everything up to the first quote the closing bracket follows.</summary>
    private static (string Value, int End)? ReadAttribute(string text, int start)
    {
        if (start >= text.Length || text[start] != '"') return null;

        for (var index = start + 1; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"' && index + 1 < text.Length && text[index + 1] == ']')
                return (text.Substring(start + 1, index - start - 1), index + 2);

            if (character is '[' or '\n' or '\r') return null;
        }

        return null;
    }

    /// <summary>
    /// Where a block the grammar did not accept begins: after the first "]" when
    /// it comes before the next "[", otherwise right after the name.
    /// </summary>
    private static int BrokenEnd(string text, int nameEnd)
    {
        for (var index = nameEnd; index < text.Length; index++)
        {
            if (text[index] == ']') return index + 1;
            if (text[index] == '[') return nameEnd;
        }

        return nameEnd;
    }

    private static bool IsTokenEnd(string text, int index) =>
        index >= text.Length || text[index] is ']' or '=' || char.IsWhiteSpace(text[index]);

    private static bool IsNameLetter(char character) => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

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
    /// The one block whose content is shown as written, which a refusal names as
    /// the way to write the markup literally. [code] is no such block: it changes
    /// the font, and the markup inside it works.
    /// </summary>
    private const string VerbatimTagName = "noparse";

    private const string VerbatimOpeningTag = "[" + VerbatimTagName + "]";

    private const string VerbatimClosingTag = "[/" + VerbatimTagName + "]";
}
