using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[li]...[/li]</c> as a reader gets it: an item of the list it stands in.
/// </summary>
/// <remarks>
/// An item written inside another item, with no list of its own between the
/// two, is drawn as a block and not as an item. A browser closes an open
/// &lt;li&gt; where the next one opens unless a list stands between them, and it
/// looks through the elements of a spoiler, a quote, an 18+ block and a hidden
/// block on the way: <c>[li]a[spoiler][li]b[/li]c[/spoiler][/li]</c> reached the
/// page as an item "a" with an empty spoiler and "b" and "c" under it, readable
/// with the spoiler closed. Drawn as a block, the inner item stays where it was
/// written, inside the spoiler.
///
/// An element that is not drawn does not count: a list or an item printed as
/// the text it was typed is no element the browser sees.
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

    /// <summary>
    /// Whether the nearest item or list drawn around the node is an item.
    /// </summary>
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
