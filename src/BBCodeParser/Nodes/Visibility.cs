// DM3 addition to the vendored BBCodeParser: this file is new.

using System;
using System.Collections.Generic;

namespace BBCodeParser.Nodes
{
    /// <summary>
    /// Which nodes one walk of the tree emits: those the caller's filter lets
    /// through, less the tags a filtered node leaves with nothing to hold.
    /// </summary>
    /// <remarks>
    /// An empty spoiler around an erased block would say something stood there.
    /// A tag typed empty, [b][/b], stays, and so does a tag printed as typed. The
    /// recursion is bounded by the depth guard of the parser.
    /// </remarks>
    internal sealed class Visibility
    {
        private readonly Func<Node, bool> filter;
        private readonly Dictionary<TagNode, bool> leftEmpty = new Dictionary<TagNode, bool>();

        private Visibility(Func<Node, bool> filter)
        {
            this.filter = filter;
        }

        /// <summary>
        /// The filter a walk applies: the caller's own, with the tags it empties
        /// left out. Null stays null: without a filter nothing is erased.
        /// </summary>
        internal static Func<Node, bool> Of(Func<Node, bool> filter) =>
            filter == null ? null : new Visibility(filter).Shows;

        private bool Shows(Node node) =>
            filter(node) && !(node is TagNode tagNode && IsLeftEmpty(tagNode));

        /// <summary>
        /// Whether the node is a tag drawn as markup that holds a node the walk
        /// leaves out and nothing else but white space.
        /// </summary>
        private bool IsLeftEmpty(TagNode node)
        {
            if (leftEmpty.TryGetValue(node, out var known))
            {
                return known;
            }

            var result = ComputeLeftEmpty(node);
            leftEmpty[node] = result;
            return result;
        }

        private bool ComputeLeftEmpty(TagNode node)
        {
            // A hiding node is drawn as the filter decides, empty or not.
            if (node.Tag.Hides || node.IsBroken || (node.Tag.RequiresClosing && !node.IsClosed))
            {
                return false;
            }

            var erased = false;
            foreach (var child in node.Children)
            {
                if (child is TextNode text)
                {
                    if (!string.IsNullOrWhiteSpace(text.Text))
                    {
                        return false;
                    }

                    continue;
                }

                if (!Shows(child))
                {
                    erased = true;
                    continue;
                }

                return false;
            }

            return erased;
        }
    }
}
