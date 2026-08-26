using BBCodeParser;
using DM.Domain.Core.Content;

namespace DM.Infrastructure.Core.Parsing;

/// <inheritdoc />
/// <remarks>
/// Answered by asking the renderer, because the renderer is the only thing that
/// knows: its depth counter runs over the tag set and counts a tag that has to
/// be closed, so a text scan reproducing it would have to carry a copy of the
/// tag set and would refuse ordinary prose the moment the copy went stale —
/// [1][2][3] footnote markers are not tags, and 512 of them are not a nesting
/// of 512.
///
/// It parses the text a second time, and that is affordable where it runs: a
/// save is rare and already writes to a database, while the render this
/// protects happens on every page view.
///
/// The comment tag set stands in for all of them. Depth is counted over tags
/// that require closing, which every surface shares; the sets differ by [mod]
/// and [private], and neither can nest inside itself — the private block is
/// refused for it by PrivateBlockMarkup.IsBalanced — so no set reaches the
/// limit at a depth another would not.
/// </remarks>
internal sealed class BbCodeNestingLimit : IBbCodeNestingLimit
{
    private readonly IBbParserProvider _parserProvider;

    /// <inheritdoc cref="BbCodeNestingLimit"/>
    public BbCodeNestingLimit(IBbParserProvider parserProvider) => _parserProvider = parserProvider;

    /// <inheritdoc />
    public bool IsWithinLimit(string? source)
    {
        if (string.IsNullOrEmpty(source)) return true;

        try
        {
            _parserProvider.GetForSurface(BbSurface.Comment).Parse(source);
            return true;
        }
        catch (BbParserException)
        {
            return false;
        }
    }
}
