using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[li]...[/li]</c> as a reader gets it: an item of the list it stands in.
/// </summary>
/// <remarks>
/// An item inside another item with no list between them is drawn as a block: a
/// browser closes the open &lt;li&gt; where the next opens, through a spoiler too,
/// and the inner item would leave it. A tag printed as typed does not count.
/// </remarks>
internal sealed class ListItemTag : Tag
{
    private const string ItemOpen = "<li>";
    private const string ItemClose = "</li>";
    private const string NestedOpen = "<div class=\"bb-li\">";
    private const string NestedClose = "</div>";

    internal ListItemTag() : base("li", ItemOpen, ItemClose)
    {
    }

    /// <inheritdoc />
    public override string GetOpenHtml(TagNode node, string attributeValue) =>
        StandsInAnItem(node) ? NestedOpen : ItemOpen;

    /// <inheritdoc />
    public override string GetCloseHtml(TagNode node, string attributeValue) =>
        StandsInAnItem(node) ? NestedClose : ItemClose;

    private static bool StandsInAnItem(TagNode node)
    {
        for (var ancestor = node.Parent; ancestor is TagNode tag; ancestor = tag.Parent)
        {
            if (tag.IsBroken || (tag.Tag.RequiresClosing && !tag.IsClosed))
            {
                continue;
            }

            if (tag.Tag is ListTag)
            {
                return false;
            }

            if (tag.Tag is ListItemTag)
            {
                return true;
            }
        }

        return false;
    }
}
