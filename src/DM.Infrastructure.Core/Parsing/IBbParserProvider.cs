using BBCodeParser;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// BBCode parsers provider. The surface is the only selector: every caller
/// names the surface its text came from, the search indexer included.
/// </summary>
public interface IBbParserProvider
{
    /// <summary>
    /// Return the parser carrying the tag set of the given surface. A tag the
    /// surface does not allow is not a parse error: the parser does not
    /// recognise it as a tag at all and leaves it in the output as the text the
    /// author typed.
    /// </summary>
    IBbParser GetForSurface(BbSurface surface);

    /// <summary>Return the NSFW-safe variant of the parser for the given surface.</summary>
    IBbParser GetSafeForSurface(BbSurface surface);

    /// <summary>
    /// Return the AuthorEdit variant for the given surface — same allowed
    /// tag set as <see cref="GetForSurface"/>, but privacy-sensitive tags
    /// ([private] / [mod]) emit <c>data-bb-*</c> attributes so Tiptap
    /// can round-trip them back to BBCode on save.
    /// </summary>
    IBbParser GetForAuthorEdit(BbSurface surface);
}
