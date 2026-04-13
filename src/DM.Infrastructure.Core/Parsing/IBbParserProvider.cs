using BBCodeParser;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// BBCode parsers provider. Surfaces are the primary selector for new code;
/// the legacy "Current*" properties are retained for callers that have not
/// yet been migrated to the surface model.
/// </summary>
public interface IBbParserProvider
{
    /// <summary>
    /// Return the parser that enforces the correct tag set for the given
    /// surface. Writing code with an unsupported tag in a surface raises
    /// a <see cref="BBCodeParser.BbParserException"/> at parse time.
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

    /// <summary>
    /// General parser for every default case
    /// </summary>
    IBbParser CurrentCommon { get; }

    /// <summary>
    /// Parser for game information
    /// </summary>
    IBbParser CurrentInfo { get; }

    /// <summary>
    /// Parser for private chat messages
    /// </summary>
    IBbParser CurrentChatMessage { get; }

    /// <summary>
    /// Parser for game posts
    /// </summary>
    IBbParser CurrentPost { get; }

    /// <summary>
    /// Parser for game posts (NSFW)
    /// </summary>
    IBbParser CurrentSafePost { get; }

    /// <summary>
    /// Parser for post reviews (NSFW)
    /// </summary>
    IBbParser CurrentSafeRating { get; }

    /// <summary>
    /// Parser for chat messages
    /// </summary>
    IBbParser CurrentGeneralChat { get; }
}