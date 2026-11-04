using BBCodeParser;
using DM.Domain.Core.Content;

namespace DM.Infrastructure.Core.Parsing;

/// <inheritdoc />
/// <remarks>
/// Asks the parser, whose depth counter knows the tag set: a text scan would count
/// [1][2][3] as nesting. The comment set stands in for all: the sets differ only
/// by [mod] and [private], and neither nests in itself.
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
