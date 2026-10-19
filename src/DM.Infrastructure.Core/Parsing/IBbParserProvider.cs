namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// BBCode parsers provider. The surface is the only selector: every caller
/// names the surface its text came from, the search indexer included.
/// </summary>
public interface IBbParserProvider
{
    /// <summary>
    /// The parser of the tag set of the surface. A tag the surface does not allow
    /// is text; hiding markup is read everywhere, so that the filter erases it.
    /// </summary>
    BbParserWrapper GetForSurface(BbSurface surface);

    /// <summary>Return the NSFW-safe variant of the parser for the given surface.</summary>
    BbParserWrapper GetSafeForSurface(BbSurface surface);
}
