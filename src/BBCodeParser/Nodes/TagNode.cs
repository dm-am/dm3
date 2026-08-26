// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System;
using System.Collections.Generic;
using System.Text;
using BBCodeParser.Tags;

namespace BBCodeParser.Nodes
{
    public class TagNode : Node
    {
        public string AttributeValue { get; }
        public Tag Tag { get; }

        public TagNode(Tag tag, Node parent, string attributeValue)
        {
            Tag = tag;
            AttributeValue = attributeValue;
            ChildNodes = new List<Node>();
            ParentNode = parent;
        }

        // DM3 change against the original: the three recursive renderers
        // (ToHtml, ToText, ToBb) are folded into this one non-recursive step.
        // See Node.cs for why. The rules below are the original ones, moved:
        //
        //   - code tags emit their subtree as BBCode source, in every mode;
        //   - preformatted tags turn alias substitutions off for their subtree;
        //   - ToText emits no markup of its own for the tag itself.
        //
        // Two rules are deliberately not carried over verbatim.
        //
        // ToHtml used to hand a code tag's children to ToBb without the filter,
        // while ToText handed them the filter. The filter is now applied
        // everywhere. Nothing in the output moves, because a code tag's
        // children are always text nodes (BbParser keeps the raw match inside
        // one), and it removes a spelling in which a permission filter would
        // silently not run.
        //
        // A list tag used to drop its text children on the HTML path outright,
        // so a sentence written between two items was stored, handed back to
        // its author on the next edit, kept in the text projection - and absent
        // from the page nobody but the author could compare against. It is
        // printed now; EmitList says where and why there.
        internal override void Emit(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitMode mode,
            Dictionary<string, string> securitySubstitutions,
            Dictionary<string, string> aliasSubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue)
        {
            var attributeValue = filterAttributeValue == null
                ? AttributeValue
                : filterAttributeValue(this, AttributeValue);

            if (mode == EmitMode.Html && Tag is ListTag)
            {
                EmitList(output, pending, aliasSubstitutions, filter, attributeValue);
                return;
            }

            switch (mode)
            {
                case EmitMode.Html:
                    output.Append(Tag.GetOpenHtml(attributeValue));
                    if (Tag.RequiresClosing)
                    {
                        pending.Push(new EmitStep(Tag.GetCloseHtml(attributeValue)));
                    }

                    break;
                case EmitMode.Bb:
                    output.Append(Tag.WithAttribute && !string.IsNullOrEmpty(attributeValue)
                        ? $@"[{Tag.Name}=""{attributeValue}""]"
                        : $@"[{Tag.Name}]");
                    if (Tag.RequiresClosing)
                    {
                        pending.Push(new EmitStep($@"[/{Tag.Name}]"));
                    }

                    break;
            }

            var childMode = mode;
            var childAliasSubstitutions = aliasSubstitutions;
            if (mode != EmitMode.Bb)
            {
                if (Tag is CodeTag)
                {
                    childMode = EmitMode.Bb;
                }
                else if (Tag is PreformattedTag)
                {
                    childAliasSubstitutions = null;
                }
            }

            PushChildren(pending, childMode, childAliasSubstitutions, filter);
        }

        /// <summary>
        /// Emit a list on the HTML path, lifting the text written between its
        /// items out of the list markup instead of dropping it.
        /// </summary>
        /// <remarks>
        /// A ul or an ol may contain nothing but li elements, so a sentence
        /// between two items has no place inside the list and cannot be given
        /// one without inventing meaning. Wrapped in an li of its own it turns
        /// into an item the author never wrote - a bullet, and in an ordered
        /// list a number, indistinguishable from the real ones. Moved to the
        /// end of the list it stops standing next to the item it comments on.
        ///
        /// So the markup steps aside for it: the list wraps each run of items
        /// and nothing else, and the text is emitted between those runs, at the
        /// point where it was written. Given
        /// <c>[ul]before[li]one[/li]between[li]two[/li]after[/ul]</c> the page
        /// gets
        /// <c>before&lt;ul&gt;&lt;li&gt;one&lt;/li&gt;&lt;/ul&gt;between&lt;ul&gt;&lt;li&gt;two&lt;/li&gt;&lt;/ul&gt;after</c>.
        /// Every word appears in its place and every ul holds only li. One
        /// thing is paid for it, by ordered lists alone: a split one starts
        /// counting from the beginning again. The alternative was not showing
        /// the text at all.
        ///
        /// Whitespace between items is not text of this kind. It is how the
        /// source was laid out, and a list written one item per line carries a
        /// newline between every pair of them. It keeps being dropped, for the
        /// reason it always was: alias substitution turns each of those into a
        /// line break, and a list would render as a column of blank lines with
        /// its items scattered down it. The same goes for the whitespace around
        /// text that is kept - it is trimmed off, being the same source layout.
        ///
        /// A list left with nothing to wrap - no children, or none but dropped
        /// whitespace - still emits its own empty markup, so an empty
        /// <c>[ul][/ul]</c> renders exactly as it did before. A list whose every
        /// child is text emits no list markup at all: there is no item for it to
        /// wrap, and an empty box around nothing is not what the author wrote.
        /// </remarks>
        private void EmitList(
            StringBuilder output,
            Stack<EmitStep> pending,
            Dictionary<string, string> aliasSubstitutions,
            Func<Node, bool> filter,
            string attributeValue)
        {
            var openHtml = Tag.GetOpenHtml(attributeValue);
            var closeHtml = Tag.RequiresClosing ? Tag.GetCloseHtml(attributeValue) : null;

            var steps = new List<EmitStep>();
            var listIsOpen = false;

            foreach (var childNode in ChildNodes)
            {
                if (filter != null && !filter(childNode))
                {
                    continue;
                }

                if (childNode is TextNode textNode)
                {
                    var betweenItems = textNode.Text.Trim();
                    if (betweenItems.Length == 0)
                    {
                        continue;
                    }

                    if (listIsOpen)
                    {
                        if (closeHtml != null)
                        {
                            steps.Add(new EmitStep(closeHtml));
                        }

                        listIsOpen = false;
                    }

                    steps.Add(new EmitStep(new TextNode(betweenItems), EmitMode.Html, aliasSubstitutions));
                    continue;
                }

                if (!listIsOpen)
                {
                    steps.Add(new EmitStep(openHtml));
                    listIsOpen = true;
                }

                steps.Add(new EmitStep(childNode, EmitMode.Html, aliasSubstitutions));
            }

            if (listIsOpen && closeHtml != null)
            {
                steps.Add(new EmitStep(closeHtml));
            }

            if (steps.Count == 0)
            {
                output.Append(openHtml);
                if (closeHtml != null)
                {
                    output.Append(closeHtml);
                }

                return;
            }

            // Pushed back to front, for the reason PushChildren is: a stack
            // hands its items back in reverse.
            for (var i = steps.Count - 1; i >= 0; i--)
            {
                pending.Push(steps[i]);
            }
        }
    }
}
