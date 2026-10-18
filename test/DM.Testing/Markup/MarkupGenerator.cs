using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DM.Infrastructure.Core.Parsing;

namespace DM.Testing.Markup;

/// <summary>
/// A stream of numbers that is the same for the same seed on every machine and
/// every runtime: SplitMix64.
/// </summary>
/// <remarks>
/// System.Random promises no such thing across versions, and the corpus the
/// client is checked against is written from these numbers, so a runtime that
/// drew them differently would read as a renderer that changed.
/// </remarks>
public sealed class MarkupRandom(ulong seed)
{
    private ulong _state = seed;

    /// <summary>The next 64 bits.</summary>
    public ulong NextBits()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A number from 0 to <paramref name="bound"/> less one.</summary>
    public int Next(int bound) => (int)(NextBits() % (ulong)bound);

    /// <summary>One item of the list.</summary>
    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    /// <summary>FNV-1a over the UTF-16 code units: a seed from a word, the same on every runtime.</summary>
    public static ulong SeedOf(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }

        return hash;
    }

    /// <summary>The seed of one case of a check: the base seed and the case number mixed.</summary>
    public static ulong CaseSeed(ulong baseSeed, int index) =>
        new MarkupRandom(baseSeed ^ ((ulong)index * 0xD1B54A32D192ED03UL)).NextBits();
}

/// <summary>
/// One generated text, as the pieces it was assembled from.
/// </summary>
/// <remarks>
/// Kept in pieces so that a failing text can be made smaller a piece at a time
/// (see MarkupProperty) rather than a character at a time, which would cut tags
/// in half and report a text the generator never makes.
/// </remarks>
public sealed class MarkupSample(IReadOnlyList<string> atoms)
{
    /// <summary>The pieces in order.</summary>
    public IReadOnlyList<string> Atoms { get; } = atoms;

    /// <summary>The text.</summary>
    public string Text { get; } = string.Concat(atoms);

    /// <inheritdoc />
    public override string ToString() => MarkupLiteral.Of(Text);
}

/// <summary>
/// Texts assembled at random from the shared alphabet (markupAlphabet.ts).
/// </summary>
/// <remarks>
/// Two kinds. A soup is anything: tags in pairs around what they hold, now and
/// then left open, among pieces of every kind, broken ones included. A valid
/// text is markup the grammar accepts whole on the surface it is written for,
/// which the editor's check must pass in silence and the save path must take.
///
/// Every word is the letter Q, four digits and Q again, each one different
/// within a text: the checks follow each word to the readers it reaches.
/// </remarks>
public sealed class MarkupGenerator
{
    private const int MaxDepth = 4;

    private readonly MarkupAlphabet _alphabet;
    private readonly MarkupRandom _random;
    private readonly List<string> _atoms = [];
    private readonly bool _hiding;
    private int _words;

    private MarkupGenerator(MarkupRandom random, bool hiding)
    {
        _alphabet = MarkupAlphabet.Shared;
        _random = random;
        _hiding = hiding;
    }

    /// <summary>A soup of pieces of every kind.</summary>
    /// <param name="random">Where the choices come from</param>
    /// <param name="hiding">
    /// False to leave out every piece that spells [private]: a text for a surface
    /// that has no hidden block, where the save path refuses any of it.
    /// </param>
    public static MarkupSample Soup(MarkupRandom random, bool hiding = true)
    {
        var generator = new MarkupGenerator(random, hiding);
        generator.Soup(0);
        return new MarkupSample(generator._atoms.ToArray());
    }

    /// <summary>Markup the grammar accepts whole on the surface.</summary>
    public static MarkupSample Valid(MarkupRandom random, BbSurface surface)
    {
        var generator = new MarkupGenerator(random, hiding: true);
        generator.Valid(0, surface, insidePrivate: false);
        return new MarkupSample(generator._atoms.ToArray());
    }

    /// <summary>A word of a generated text, the n-th.</summary>
    public static string Word(int number) => $"Q{number.ToString("D4", CultureInfo.InvariantCulture)}Q";

    private string NextWord() => Word(++_words);

    private void Soup(int depth)
    {
        var count = 1 + _random.Next(5);
        for (var index = 0; index < count; index++)
        {
            var roll = _random.Next(100);
            if (depth < MaxDepth && roll < 30)
            {
                var (open, close) = Allowed(_alphabet.Pairs, pair => pair.Open + pair.Close);
                _atoms.Add(Expand(open));
                Soup(depth + 1);
                if (_random.Next(8) != 0) _atoms.Add(close);
            }
            else if (roll < 35)
            {
                var (open, close) = Allowed(_alphabet.Leaves, pair => pair.Open);
                _atoms.Add(open);
                _atoms.Add(_random.Pick(_alphabet.Addresses));
                _atoms.Add(close);
            }
            else if (roll < 55)
            {
                _atoms.Add(NextWord());
            }
            else if (roll < 57)
            {
                var (piece, times) = Allowed(_alphabet.Repeats, repeat => repeat.Piece);
                _atoms.Add(string.Concat(Enumerable.Repeat(piece, times)));
            }
            else
            {
                _atoms.Add(Expand(Allowed(_alphabet.Pieces, piece => piece)));
            }
        }
    }

    private void Valid(int depth, BbSurface surface, bool insidePrivate)
    {
        var count = 1 + _random.Next(4);
        for (var index = 0; index < count; index++)
        {
            var roll = _random.Next(100);
            var nests = depth < MaxDepth;
            if (nests && roll < 22)
            {
                var (open, close) = _random.Pick(_alphabet.ValidPairs);
                _atoms.Add(open);
                Valid(depth + 1, surface, insidePrivate);
                _atoms.Add(close);
            }
            else if (nests && roll < 27)
            {
                var (open, close) = _random.Pick(_alphabet.ValidLists);
                _atoms.Add(open);
                var items = 1 + _random.Next(3);
                for (var item = 0; item < items; item++)
                {
                    if (_random.Next(2) == 0) _atoms.Add("\n");
                    var (itemOpen, itemClose) = _random.Pick(_alphabet.ValidItems);
                    _atoms.Add(itemOpen);
                    Valid(depth + 1, surface, insidePrivate);
                    _atoms.Add(itemClose);
                }

                if (_random.Next(2) == 0) _atoms.Add("\n");
                _atoms.Add(close);
            }
            else if (nests && roll < 30 && surface is BbSurface.Comment or BbSurface.GlobalChatMessage)
            {
                var (open, close) = _random.Pick(_alphabet.ValidMod);
                _atoms.Add(open);
                Valid(depth + 1, surface, insidePrivate);
                _atoms.Add(close);
            }
            else if (nests && roll < 36 && surface == BbSurface.GamePost && !insidePrivate)
            {
                _atoms.Add(Expand(_random.Pick(_alphabet.ValidPrivate)));
                Valid(depth + 1, surface, insidePrivate: true);
                _atoms.Add(_random.Pick(_alphabet.ValidPrivateClosings));
            }
            else if (roll < 40)
            {
                _atoms.Add("[noparse]");
                var pieces = 1 + _random.Next(4);
                for (var piece = 0; piece < pieces; piece++)
                {
                    _atoms.Add(_random.Next(3) == 0
                        ? NextWord()
                        : Expand(Allowed(_alphabet.Pieces, candidate => candidate, insideExample: true)));
                }

                _atoms.Add("[/noparse]");
            }
            else if (roll < 48)
            {
                _atoms.Add(_random.Pick(_alphabet.ValidLeaves));
            }
            else if (roll < 72)
            {
                _atoms.Add(NextWord());
            }
            else
            {
                _atoms.Add(_random.Pick(_alphabet.ValidText));
            }
        }
    }

    /// <summary>
    /// An item of the list that the text may hold: without [private] when the
    /// text has none, and inside an example without anything that could end it.
    /// </summary>
    private T Allowed<T>(IReadOnlyList<T> items, Func<T, string> text, bool insideExample = false)
    {
        while (true)
        {
            var item = _random.Pick(items);
            var read = MarkupText.AsBothSidesRead(text(item)).ToLowerInvariant();
            if (!_hiding && read.Contains("private", StringComparison.Ordinal)) continue;
            if (insideExample && read.Contains("[/noparse", StringComparison.Ordinal)) continue;
            return item;
        }
    }

    /// <summary>The piece with its placeholders filled: a list of names, a surrogate with no partner.</summary>
    private string Expand(string piece) =>
        piece.Contains('{', StringComparison.Ordinal)
            ? piece.Replace("{names}", _random.Pick(_alphabet.Names), StringComparison.Ordinal)
                .Replace("{high-surrogate}", "\uD800", StringComparison.Ordinal)
                .Replace("{low-surrogate}", "\uDC00", StringComparison.Ordinal)
            : piece;
}

/// <summary>
/// The text both sides read, and a way to print one.
/// </summary>
public static class MarkupText
{
    /// <summary>
    /// The text without what both sides take out before reading it: the C0 and
    /// C1 control characters other than tab, line feed and carriage return, and
    /// the characters that reorder text for display, U+202A..U+202E and
    /// U+2066..U+2069; a surrogate with no partner becomes U+FFFD.
    /// </summary>
    /// <remarks>
    /// Written out from BBCODE_RENDERING.md, section "Символы направления текста",
    /// rather than borrowed from the parser: a range the parser got wrong would
    /// otherwise be a range the checks got wrong with it.
    /// </remarks>
    public static string AsBothSidesRead(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is < ' ' and not ('\t' or '\n' or '\r') or >= '\u007F' and <= '\u009F' or
                >= '\u202a' and <= '\u202e' or >= '\u2066' and <= '\u2069')
            {
                continue;
            }

            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                builder.Append(character).Append(text[index + 1]);
                index++;
                continue;
            }

            builder.Append(char.IsSurrogate(character) ? '\ufffd' : character);
        }

        return builder.ToString();
    }
}

/// <summary>A text as a string literal both C# and TypeScript read back as the same text.</summary>
public static class MarkupLiteral
{
    /// <summary>
    /// Quoted, with every character the eye cannot tell apart written as a \u
    /// escape: control characters, spaces other than the plain one, the marks
    /// and the characters that reorder text, surrogates, and everything outside
    /// ASCII and Cyrillic.
    /// </summary>
    public static string Of(string text)
    {
        var builder = new StringBuilder(text.Length + 2).Append('"');
        foreach (var character in text)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character is >= ' ' and <= '~' and not '?' ||
                        character is >= '\u0410' and <= '\u044F')
                    {
                        builder.Append(character);
                    }
                    else
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}
