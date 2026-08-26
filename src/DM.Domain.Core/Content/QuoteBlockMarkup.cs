using System;

namespace DM.Domain.Core.Content;

/// <summary>
/// The shape of the [quote] block, in one place.
/// </summary>
/// <remarks>
/// The tag carries one value and only one: the name of whoever is being quoted,
/// as a snapshot taken at the moment the quotation was made. It is deliberately
/// not resolved when the text is read - a quotation is what the quoter quoted,
/// and renaming a character afterwards must not rewrite somebody else's line.
///
/// There is no second value. A link back to the source would need one, and the
/// grammar that carries two values is its own piece of work; the tag stays at
/// one until that work is done, and a quotation without a source stays a legal
/// form regardless, because the whole imported corpus arrives in exactly that
/// shape - on DM2 the tag has no attribute at all.
/// </remarks>
public static class QuoteBlockMarkup
{
    /// <summary>Tag name of the quotation block.</summary>
    public const string TagName = "quote";

    /// <summary>
    /// Wrap a body in a quotation, attributed to <paramref name="authorName"/>.
    /// </summary>
    /// <remarks>
    /// No name means no attribute, and no attribute means no header when the
    /// block is drawn. That is the ordinary case rather than a corner one: the
    /// DM2 tag has no attribute, so every quotation of the imported archive is
    /// a quotation without an author.
    ///
    /// The body is put on its own lines. A quotation of a single line reads the
    /// same either way, but one that starts with a list or a block of its own
    /// does not: the opening tag and the first item would share a line and the
    /// author would meet the seam of the markup rather than the text.
    /// </remarks>
    /// <param name="body">Quotation body, already filtered for the reader</param>
    /// <param name="authorName">
    /// Name to attribute the quotation to, or null / blank for no attribution
    /// </param>
    public static string Compose(string body, string? authorName)
    {
        var name = SanitiseAuthorName(authorName);
        var open = name.Length == 0 ? $"[{TagName}]" : $"[{TagName}=\"{name}\"]";
        return $"{open}\n{body}\n[/{TagName}]";
    }

    /// <summary>
    /// A name the tag can actually carry.
    /// </summary>
    /// <remarks>
    /// One character has to go and only one: the opening bracket. Every spelling
    /// of an attribute value ends at it, because it is what starts the next tag,
    /// so a name containing one is not a value the parser will read - the tag
    /// stops being a tag and the reader is shown its literal text. Everything
    /// else a name may hold survives the quoted form as written: spaces, a
    /// closing bracket, an ampersand, and a double quote too, including one at
    /// the very end of the name, where the value class stops one character early
    /// and the closing quote of the attribute takes over.
    ///
    /// Line breaks become spaces. The quoted form would carry one, but a name is
    /// written into a single-line field and a line break in it is damage rather
    /// than content - and a quotation tag split across lines is unreadable in
    /// the composer the author is about to type into.
    ///
    /// A name reduced to nothing by all this is no name, and the block is
    /// composed without an attribute, exactly as if none had been given.
    /// </remarks>
    private static string SanitiseAuthorName(string? authorName)
    {
        if (string.IsNullOrWhiteSpace(authorName)) return string.Empty;

        Span<char> buffer = authorName.Length <= 256
            ? stackalloc char[authorName.Length]
            : new char[authorName.Length];

        var length = 0;
        foreach (var character in authorName)
        {
            if (character == '[') continue;
            buffer[length++] = character is '\r' or '\n' ? ' ' : character;
        }

        return new string(buffer[..length]).Trim();
    }
}
