using BBCodeParser.Nodes;
using BBCodeParser.Tags;
using DM.Domain.Core.Content;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[private="..."]...[/private]</c> as a reader gets it: the text, carrying
/// in an attribute who it is for, which the page shows in a tooltip.
/// </summary>
/// <remarks>
/// The recipients are not text of the post: the tooltip over the block shows them,
/// so they ride on the block and go wherever it goes (<see cref="PrivateRecipients"/>).
/// The block stands in the line and ends none.
/// </remarks>
internal sealed class PrivateTag : Tag
{
    internal PrivateTag(string openTag, string closeTag)
        : base(PrivateBlockMarkup.TagName, openTag, closeTag, withAttribute: true, sealedByOwnTag: true)
    {
        Hides = true;
    }

    /// <inheritdoc />
    /// <remarks>The names the block is for, as one comma-separated value.</remarks>
    public override string GetOpenHtml(TagNode node, string attributeValue) =>
        base.GetOpenHtml(node, PrivateRecipients.Line((attributeValue ?? string.Empty).Split(',')));
}
