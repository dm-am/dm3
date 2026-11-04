// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using BBCodeParser.Tags;

namespace BBCodeParser.Nodes
{
    public class TagNode : Node
    {
        public string AttributeValue { get; }
        public Tag Tag { get; }

        // DM3 addition against the original: the named parameters of the opening tag.
        public IReadOnlyDictionary<string, string> Parameters { get; }

        // DM3 addition against the original: whether the opening tag did not match
        // the grammar. Such a node renders as typed, except a hiding one (Tag.Hides).
        public bool IsBroken { get; }

        // DM3 addition against the original: whether its own closing tag ended the
        // node. One that was not closed renders as typed.
        public bool IsClosed { get; private set; }

        // DM3 addition against the original: both tags as they stand in the source.
        public string OpeningText { get; }
        public string ClosingText { get; private set; }

        // The constructor of the original, for a tree built by hand: such a node
        // counts as closed.
        public TagNode(Tag tag, Node parent, string attributeValue)
            : this(tag, parent, attributeValue, null, false, null)
        {
            IsClosed = true;
        }

        public TagNode(Tag tag, Node parent, string attributeValue, IReadOnlyDictionary<string, string> parameters,
            bool isBroken, string openingText)
        {
            Tag = tag;
            AttributeValue = attributeValue;
            Parameters = parameters ?? ReadOnlyDictionary<string, string>.Empty;
            IsBroken = isBroken;
            OpeningText = openingText;
            ChildNodes = new List<Node>();
            ParentNode = parent;
        }

        internal void Close(string closingText)
        {
            IsClosed = true;
            ClosingText = closingText;
        }

        // DM3 change against the original: the three renderers folded into one
        // step. The filter now reaches the children of a code tag as well, and a
        // list prints the text between its items (EmitList).
        internal override void Emit(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitStep step,
            Dictionary<string, string> securitySubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue)
        {
            var mode = step.Mode;

            if (RendersAsWritten(filter, out var content))
            {
                EmitAsWritten(output, pending, step, securitySubstitutions, filter);
                return;
            }

            if (Tag is ContentTag contentTag)
            {
                EmitContent(output, mode, contentTag, content, filter);
                return;
            }

            var attributeValue = filterAttributeValue == null
                ? AttributeValue
                : filterAttributeValue(this, AttributeValue);

            if (mode == EmitMode.Html && Tag is ListTag)
            {
                EmitList(pending, step, filter, attributeValue);
                return;
            }

            switch (mode)
            {
                case EmitMode.Html:
                    output.Append(Tag.GetOpenHtml(this, attributeValue));
                    if (Tag.RequiresClosing)
                    {
                        pending.Push(new EmitStep(Tag.GetCloseHtml(this, attributeValue), Tag.DropsLineBreakAfter));
                    }

                    break;
                case EmitMode.Bb:
                    output.Append(OpeningText ?? RebuildOpening(attributeValue));
                    if (Tag.RequiresClosing)
                    {
                        pending.Push(new EmitStep(ClosingAsTyped));
                    }

                    break;
            }

            var childMode = mode;
            var childAliasSubstitutions = step.AliasSubstitutions;
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

        private string RebuildOpening(string attributeValue) =>
            Tag.WithAttribute && !string.IsNullOrEmpty(attributeValue)
                ? $@"[{Tag.Name}=""{attributeValue}""]"
                : $@"[{Tag.Name}]";

        private string OpeningAsTyped => OpeningText ?? RebuildOpening(AttributeValue);

        private string ClosingAsTyped => ClosingText ?? $@"[/{Tag.Name}]";

        // DM3 addition against the original: a broken tag, an unclosed one and a
        // content tag without an accepted value render as typed. A hiding node
        // stays a block.
        private bool RendersAsWritten(Func<Node, bool> filter, out string content)
        {
            content = null;
            if (Tag.Hides)
            {
                return false;
            }

            if (IsBroken || (Tag.RequiresClosing && !IsClosed))
            {
                return true;
            }

            if (Tag is not ContentTag contentTag)
            {
                return false;
            }

            content = ReadContent(contentTag, filter);
            return content == null;
        }

        /// <summary>
        /// The value of a closed content tag, or null when the node holds anything
        /// but text or text the tag does not accept.
        /// </summary>
        /// <remarks>
        /// DM3 addition against the original: what the filter leaves out is not
        /// held, so an erased block inside a link does not reveal itself.
        /// </remarks>
        private string ReadContent(ContentTag contentTag, Func<Node, bool> filter)
        {
            var raw = new StringBuilder();
            foreach (var child in ChildNodes)
            {
                if (filter != null && !filter(child))
                {
                    continue;
                }

                if (child is not TextNode textNode)
                {
                    return null;
                }

                raw.Append(textNode.Text);
            }

            return contentTag.ReadContent(raw.ToString());
        }

        private void EmitContent(StringBuilder output, EmitMode mode, ContentTag contentTag, string content,
            Func<Node, bool> filter)
        {
            switch (mode)
            {
                case EmitMode.Html:
                    output.Append(contentTag.RenderHtml(this, content));
                    break;
                case EmitMode.Text:
                    output.Append(contentTag.RenderText(this, content));
                    break;
                case EmitMode.Bb:
                    output.Append(OpeningAsTyped);
                    foreach (var child in ChildNodes)
                    {
                        if (filter == null || filter(child))
                        {
                            output.Append(((TextNode)child).Text);
                        }
                    }

                    output.Append(ClosingAsTyped);
                    break;
            }
        }

        /// <summary>
        /// The node as it was typed: its opening tag as text, its children in
        /// the mode of its parent, and its own closing tag as text when it had
        /// one.
        /// </summary>
        /// <remarks>
        /// The children keep the parent's mode: a broken [noparse] is no example, and
        /// a hiding block inside it still hides. Never the whole source of the node,
        /// because a hiding block inside it would be printed with it.
        /// </remarks>
        private void EmitAsWritten(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitStep step,
            Dictionary<string, string> securitySubstitutions,
            Func<Node, bool> filter)
        {
            output.Append(Literal(OpeningAsTyped, step.Mode, securitySubstitutions));
            if (IsClosed && ClosingText != null)
            {
                pending.Push(new EmitStep(Literal(ClosingText, step.Mode, securitySubstitutions)));
            }

            PushChildren(pending, step.Mode, step.AliasSubstitutions, filter);
        }

        private static string Literal(string text, EmitMode mode, Dictionary<string, string> securitySubstitutions) =>
            mode == EmitMode.Bb ? text : TextNode.SubstituteText(text, securitySubstitutions);

        /// <summary>
        /// Emit a list on the HTML path, lifting the text written between its
        /// items out of the list markup instead of dropping it.
        /// </summary>
        /// <remarks>
        /// A list may hold nothing but items, so it wraps each run of items and the
        /// text is emitted between the runs where it was written:
        /// <c>[ul]a[li]b[/li]c[/ul]</c> gives <c>a&lt;ul&gt;&lt;li&gt;b&lt;/li&gt;&lt;/ul&gt;c</c>.
        /// Whitespace between items is source layout and is dropped, or every
        /// newline would become a line break.
        /// </remarks>
        private void EmitList(
            Stack<EmitStep> pending,
            EmitStep step,
            Func<Node, bool> filter,
            string attributeValue)
        {
            var openHtml = Tag.GetOpenHtml(this, attributeValue);
            var closeHtml = Tag.RequiresClosing ? Tag.GetCloseHtml(this, attributeValue) : null;

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
                            steps.Add(new EmitStep(closeHtml, Tag.DropsLineBreakAfter));
                        }

                        listIsOpen = false;
                    }

                    steps.Add(new EmitStep(new TextNode(betweenItems), EmitMode.Html, step.AliasSubstitutions));
                    continue;
                }

                if (!listIsOpen)
                {
                    steps.Add(new EmitStep(openHtml));
                    listIsOpen = true;
                }

                steps.Add(new EmitStep(childNode, EmitMode.Html, step.AliasSubstitutions));
            }

            if (listIsOpen && closeHtml != null)
            {
                steps.Add(new EmitStep(closeHtml, Tag.DropsLineBreakAfter));
            }

            if (steps.Count == 0)
            {
                // A step, so that the empty list ends its line (Tag.DropsLineBreakAfter).
                pending.Push(new EmitStep(openHtml + closeHtml, Tag.DropsLineBreakAfter));
                return;
            }

            // Pushed back to front, as in PushChildren.
            for (var i = steps.Count - 1; i >= 0; i--)
            {
                pending.Push(steps[i]);
            }
        }
    }
}
