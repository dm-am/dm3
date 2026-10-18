using System.Text;

namespace DM.Domain.Core.Content;

/// <summary>
/// The text the markup parser reads, for the paths that have to read the same
/// text without the parser: the checks of the save path, and the search
/// projection when the parser refused a body.
/// </summary>
/// <remarks>
/// The parser does not read a body as it is stored. It first removes the C0 and
/// C1 control characters other than tab, line feed and carriage return, and the
/// characters that reverse the order text is drawn in (U+202A..U+202E,
/// U+2066..U+2069), and replaces a surrogate with no partner with U+FFFD
/// (BbParser, "what a character cannot be"). A check that reads the stored text
/// reads a different text, and the difference is not harmless: removing a
/// character joins what stood on either side of it, and the join can be a tag
/// the check never saw. "[m" U+0001 "od]" was no marker to the sanitizer and a
/// moderator block on the page; "[code" U+0001 "]" was no code block to the
/// balance check and a code block that printed the hidden block under it to the
/// whole room.
///
/// So the save path reads what the parser reads. The rule is written twice, here
/// and in the parser, because a domain assembly references no parser;
/// ControlCharacterMarkupShould holds the two to the same answer for every
/// control, whitespace, format and surrogate character in every place of a tag.
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
