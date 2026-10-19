using System.Text;

namespace DM.Domain.Core.Content;

/// <summary>
/// The text the markup parser reads, for the paths that have to read the same
/// text without the parser: the checks of the save path, and the search
/// projection when the parser refused a body.
/// </summary>
/// <remarks>
/// The parser removes C0 and C1 controls other than tab, line feed and carriage
/// return and the bidi overrides (U+202A..U+202E, U+2066..U+2069), and replaces a
/// lone surrogate with U+FFFD. A removal joins its neighbours into a tag a check of
/// the stored text never sees, so the save path reads what the parser reads.
/// The rule is written twice because a domain assembly references no parser;
/// ControlCharacterMarkupShould holds the two copies equal.
/// </remarks>
public static class ParserInput
{
    private const char Replacement = '\uFFFD';

    /// <summary>
    /// The text as the parser reads it; the very instance passed in when the
    /// parser would read it unchanged, which is nearly always.
    /// </summary>
    public static string Of(string text)
    {
        var first = FirstToChange(text);
        if (first < 0) return text;

        var builder = new StringBuilder(text.Length);
        builder.Append(text, 0, first);
        for (var index = first; index < text.Length; index++)
        {
            var character = text[index];
            if (IsRemoved(character)) continue;

            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                builder.Append(character).Append(text[index + 1]);
                index++;
                continue;
            }

            builder.Append(char.IsSurrogate(character) ? Replacement : character);
        }

        return builder.ToString();
    }

    private static int FirstToChange(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (IsRemoved(character)) return index;
            if (!char.IsSurrogate(character)) continue;

            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
                continue;
            }

            return index;
        }

        return -1;
    }

    private static bool IsRemoved(char character) =>
        (char.IsControl(character) && character is not ('\t' or '\n' or '\r')) ||
        character is >= '\u202A' and <= '\u202E' or >= '\u2066' and <= '\u2069';
}
