using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Infrastructure.Core.Parsing;

namespace DM.Testing.Markup;

/// <summary>Who reads a post, in the words of the visibility rule.</summary>
public enum HiddenTextReader
{
    /// <summary>A reader of the room with no claim to any block.</summary>
    Stranger,

    /// <summary>The author of the post.</summary>
    Author,

    /// <summary>A master or an assistant of the game.</summary>
    Lead,

    /// <summary>The owner of the character "Вася".</summary>
    Addressee,

    /// <summary>A reader of a room that shows private text to every reader.</summary>
    RoomOverride,

    /// <summary>A reader of a game that shows every hidden block to every reader.</summary>
    GameOverride
}

/// <summary>A [private] block as the rule reads it.</summary>
/// <param name="Start">Where its opening tag starts.</param>
/// <param name="OpeningEnd">Where its content starts.</param>
/// <param name="End">Where its closing tag ends, or the end of the text.</param>
/// <param name="WellSpelled">Written as [private] or [private="names"].</param>
/// <param name="Names">The names of its addressee list, trimmed, without the empty ones.</param>
/// <param name="Closed">Its own closing tag ends it.</param>
public sealed record HiddenBlock(int Start, int OpeningEnd, int End, bool WellSpelled, IReadOnlyList<string> Names,
    bool Closed)
{
    /// <summary>Whether the reader reads this block on the surface, by BBCODE_RENDERING.md, "Матрица видимости".</summary>
    public bool IsReadBy(HiddenTextReader reader, BbSurface surface) =>
        surface == BbSurface.GamePost && reader switch
        {
            HiddenTextReader.Author or HiddenTextReader.Lead => true,
            HiddenTextReader.Addressee => WellSpelled &&
                                          Names.Contains(HiddenTextOracle.AddresseeName, StringComparer.OrdinalIgnoreCase),
            HiddenTextReader.RoomOverride or HiddenTextReader.GameOverride => WellSpelled && Names.Count > 0,
            _ => false
        };
}

/// <summary>A word of a generated text, and the blocks it stands in, outermost first.</summary>
/// <param name="Word">The word.</param>
/// <param name="Blocks">The blocks around it.</param>
/// <param name="InOpeningTag">It stands in the opening tag of the innermost block, not in its content.</param>
public sealed record HiddenTextWord(string Word, IReadOnlyList<HiddenBlock> Blocks, bool InOpeningTag)
{
    /// <summary>Whether the reader may read the word on the surface: every block around it lets them in.</summary>
    public bool IsReadBy(HiddenTextReader reader, BbSurface surface) =>
        Blocks.All(block => block.IsReadBy(reader, surface));
}

/// <summary>A generated text as the rule reads it, for the checks to hold the server and the client to.</summary>
/// <param name="Text">The text both sides read, without the characters they take out.</param>
/// <param name="Blocks">Every hidden block, in the order they open.</param>
/// <param name="Words">Every word of the text.</param>
/// <param name="GamePostRefusal">Why the save path of a game post refuses the text, or null.</param>
/// <param name="HidingMarkupAt">Where the first hiding token stands outside a closed [noparse], or -1.</param>
public sealed record HiddenTextReading(string Text, IReadOnlyList<HiddenBlock> Blocks,
    IReadOnlyList<HiddenTextWord> Words, string? GamePostRefusal, int HidingMarkupAt)
{
    /// <summary>Whether the save path of the surface refuses the text.</summary>
    public bool IsRefusedOn(BbSurface surface) =>
        surface == BbSurface.GamePost ? GamePostRefusal != null : HidingMarkupAt >= 0;
}

/// <summary>
/// The reference reading of hidden blocks: which part of a text is a [private]
/// block, who reads it, and whether the save path must refuse it, written from
/// BBCODE_RENDERING.md alone.
/// </summary>
/// <remarks>
/// Next to the parser and the save path, so checks can ask those about any text without trusting them.
/// It knows [private] and [noparse] only: nothing else changes what is hidden.
/// </remarks>
public static class HiddenTextOracle
{
    /// <summary>The character whose owner is the addressee reader.</summary>
    public const string AddresseeName = "Вася";

    private static readonly Regex WordPattern = new(@"Q\d{4}Q", RegexOptions.Compiled);

    /// <summary>Read a text.</summary>
    public static HiddenTextReading Read(string input)
    {
        var text = MarkupText.AsBothSidesRead(input);
        var blocks = new List<HiddenBlock>();
        var open = new Stack<(int Start, int OpeningEnd, bool WellSpelled, IReadOnlyList<string> Names)>();
        var noparseLeftOpen = false;
        string? refusal = null;
        var hidingAt = -1;

        void Refuse(string why, int at) => refusal ??= $"{why} at {at}";

        var position = 0;
        while ((position = text.IndexOf('[', position)) >= 0)
        {
            if (Spells(text, position, "[noparse]"))
            {
                var closing = IndexOfSpelling(text, "[/noparse]", position + "[noparse]".Length);
                if (closing >= 0)
                {
                    position = closing + "[/noparse]".Length;
                    continue;
                }

                noparseLeftOpen = true;
                position += "[noparse]".Length;
                continue;
            }

            if (IsHidingToken(text, position + 1))
            {
                if (hidingAt < 0) hidingAt = position;
                var (wellSpelled, names, openingEnd) = ReadOpening(text, position);
                if (!wellSpelled) Refuse("a block spelled wrong", position);
                if (open.Count > 0) Refuse("a block inside a block", position);
                if (noparseLeftOpen) Refuse("a hiding token after a [noparse] left open", position);
                open.Push((position, openingEnd, wellSpelled, names));
                position = openingEnd;
                continue;
            }

            if (Spells(text, position, "[/private]"))
            {
                if (hidingAt < 0) hidingAt = position;
                if (noparseLeftOpen) Refuse("a hiding token after a [noparse] left open", position);
                if (open.Count == 0)
                {
                    Refuse("a closing tag with nothing to close", position);
                }
                else
                {
                    var (start, openingEnd, wellSpelled, names) = open.Pop();
                    blocks.Add(new HiddenBlock(start, openingEnd, position + "[/private]".Length, wellSpelled, names,
                        Closed: true));
                }

                position += "[/private]".Length;
                continue;
            }

            if (position + 1 < text.Length && text[position + 1] == '/' && IsHidingToken(text, position + 2))
            {
                // [/private="x"] and the like: text, which closes nothing, and
                // hiding markup all the same on a surface that has none.
                if (hidingAt < 0) hidingAt = position;
            }

            position++;
        }

        CloseLeftOpen(open, blocks, text.Length, Refuse);
        blocks.Sort((left, right) => left.Start.CompareTo(right.Start));

        return new HiddenTextReading(text, blocks, WordsIn(text, blocks), refusal, hidingAt);
    }

    /// <summary>A block nobody closed runs to the end of the text, and the save path refuses it.</summary>
    private static void CloseLeftOpen(
        Stack<(int Start, int OpeningEnd, bool WellSpelled, IReadOnlyList<string> Names)> open,
        List<HiddenBlock> blocks, int end, Action<string, int> refuse)
    {
        while (open.Count > 0)
        {
            var (start, openingEnd, wellSpelled, names) = open.Pop();
            refuse("a block left open", start);
            blocks.Add(new HiddenBlock(start, openingEnd, end, wellSpelled, names, Closed: false));
        }
    }

    /// <summary>Every word of the text, with the blocks it stands in.</summary>
    private static HiddenTextWord[] WordsIn(string text, IReadOnlyList<HiddenBlock> blocks) =>
        WordPattern.Matches(text)
            .Select(match =>
            {
                var around = blocks.Where(block => block.Start <= match.Index && match.Index < block.End).ToArray();
                var inOpening = around.Length > 0 && match.Index < around[^1].OpeningEnd;
                return new HiddenTextWord(match.Value, around, inOpening);
            })
            .ToArray();

    /// <summary>
    /// Whether "private" in any case of A-Z starts at the index and ends a token:
    /// no letter after it, and then "]", "=", whitespace or the end.
    /// </summary>
    private static bool IsHidingToken(string text, int nameStart)
    {
        const string name = "private";
        if (nameStart + name.Length > text.Length || !Spells(text, nameStart, name)) return false;

        var after = nameStart + name.Length;
        return after == text.Length || text[after] is ']' or '=' || IsWhiteSpace(text[after]);
    }

    /// <summary>
    /// Where the opening at the index ends, whether it is well spelled and whom
    /// it names.
    /// </summary>
    private static (bool WellSpelled, IReadOnlyList<string> Names, int End) ReadOpening(string text, int start)
    {
        var nameEnd = start + "[private".Length;
        if (nameEnd < text.Length && text[nameEnd] == ']') return (true, [], nameEnd + 1);

        if (nameEnd + 1 < text.Length && text[nameEnd] == '=' && text[nameEnd + 1] == '"')
        {
            for (var index = nameEnd + 2; index < text.Length; index++)
            {
                var character = text[index];
                if (character is '[' or '\n' or '\r') break;
                if (character == '"' && index + 1 < text.Length && text[index + 1] == ']')
                {
                    var names = text[(nameEnd + 2)..index].Split(',')
                        .Select(name => name.Trim())
                        .Where(name => name.Length > 0)
                        .ToArray();
                    return (true, names, index + 2);
                }
            }
        }

        // Spelled wrong: the opening reaches the first "]" when it comes before
        // the next "[", and ends right after the name otherwise.
        for (var index = nameEnd; index < text.Length; index++)
        {
            if (text[index] == ']') return (false, [], index + 1);
            if (text[index] == '[') break;
        }

        return (false, [], nameEnd);
    }

    /// <summary>Whether the text at the index spells the lower-case spelling, A-Z folded and nothing else.</summary>
    private static bool Spells(string text, int index, string spelling)
    {
        if (index + spelling.Length > text.Length) return false;
        for (var offset = 0; offset < spelling.Length; offset++)
        {
            var character = text[index + offset];
            if (character is >= 'A' and <= 'Z') character = (char)(character - 'A' + 'a');
            if (character != spelling[offset]) return false;
        }

        return true;
    }

    private static int IndexOfSpelling(string text, string spelling, int from)
    {
        for (var index = text.IndexOf('[', from); index >= 0; index = text.IndexOf('[', index + 1))
        {
            if (Spells(text, index, spelling)) return index;
        }

        return -1;
    }

    /// <summary>
    /// White space as Unicode defines it (White_Space), listed: each language answers differently and the
    /// client copies this list.
    /// </summary>
    public static bool IsWhiteSpace(char character) => character is
        (>= '\u0009' and <= '\u000D') or '\u0020' or '\u0085' or '\u00A0' or '\u1680' or
        (>= '\u2000' and <= '\u200A') or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000';
}
