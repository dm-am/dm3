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
    /// The filter erases a block its reader may not read, and erases it without
    /// a trace: no placeholder, no gap. The tags around the block are the
    /// author's and stay, and one that held nothing but the block used to stay
    /// empty: [b][private]x[/private][/b] gave that reader an empty bold element,
    /// and [spoiler][private]x[/private][/spoiler] a spoiler with nothing in it,
    /// both of which said that something had stood there. So a tag that is
    /// drawn as markup, holds at least one node the walk leaves out and nothing
    /// else but white space, is left out as well, as if the author had not
    /// written it. What the author typed empty stays: [b][/b] is still drawn,
    /// because nothing was erased from it.
    ///
    /// A tag printed as the text it was typed (broken, left open, a content tag
    /// that holds no value it accepts) is not left out: its tags are text, and
    /// text shows the same with the block and without it.
    ///
    /// The answer for a node is remembered for the walk, so each node is looked
    /// at once however deep it stands. The recursion is bounded by the depth
    /// guard of the parser (see BbParser).
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
            // A hiding node the filter lets through is the filter's decision,
            // and it is drawn as the filter says, empty or not.
            if (node.Tag.Hides || node.IsBroken || (node.Tag.RequiresClosing && !node.IsClosed))
            {
                return false;
            }

            var erased = false;
            foreach (var child in node.Children)
            {
                if (child is TextNode text)
                {
                    if (!IsWhiteSpace(text.Text))
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

        private static bool IsWhiteSpace(string text)
        {
            foreach (var character in text)
            {
                if (!char.IsWhiteSpace(character))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
