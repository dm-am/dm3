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
        private static readonly IReadOnlyDictionary<string, string> NoParameters =
            new Dictionary<string, string>();

        public string AttributeValue { get; }
        public Tag Tag { get; }

        // DM3 addition against the original: the named parameters the opening
        // tag was written with, when the grammar accepted it.
        public IReadOnlyDictionary<string, string> Parameters { get; }

        // DM3 addition against the original: whether the opening tag did not
        // match the grammar of its tag. Such a node renders as the text that was
        // typed - see EmitAsWritten - except a hiding one, which stays a block
        // and is shown to fewer readers (see Tag.Hides).
        public bool IsBroken { get; }

        // DM3 addition against the original: whether the node's own closing tag
        // ended it. A node that ran to the end of the text, or whose closing tag
        // never came, is not closed, and renders as the text that was typed.
        public bool IsClosed { get; private set; }

        // DM3 addition against the original: the opening and the closing tag as
        // they stand in the source, for the node that renders as written.
        public string OpeningText { get; }
        public string ClosingText { get; private set; }

        // The constructor of the original, for a tree built by hand rather than
        // parsed. Such a node is the markup its builder asked for, as it was in
        // the original: nobody left it open, so it counts as closed.
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
            Parameters = parameters ?? NoParameters;
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

        // DM3 change against the original: the three recursive renderers
        // (ToHtml, ToText, ToBb) are folded into this one non-recursive step.
        // See Node.cs for why. The rules below are the original ones, moved:
        //
        //   - code tags emit their subtree as BBCode source, in every mode (in
        //     DM3 the one code tag is [noparse], see CodeTag);
        //   - preformatted tags turn alias substitutions off for their subtree;
        //   - ToText emits no markup of its own for the tag itself.
        //
        // Two rules are deliberately not carried over verbatim.
        //
        // ToHtml used to hand a code tag's children to ToBb without the filter,
        // while ToText handed them the filter. The filter is now applied
        // everywhere. Nothing in the output moves, because a code tag that
        // renders as one holds a single text node (BbParser reads its content
        // as one piece), and it removes a spelling in which a permission filter
        // would silently not run.
        //
        // A list tag used to drop its text children on the HTML path outright,
        // so a sentence written between two items was stored, handed back to
        // its author on the next edit, kept in the text projection - and absent
        // from the page nobody but the author could compare against. It is
        // printed now; EmitList says where and why there.
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
                EmitList(output, pending, step, filter, attributeValue);
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
                        pending.Push(new EmitStep(ClosingText ?? $@"[/{Tag.Name}]"));
                    }

                    break;
                case EmitMode.BbInLowerCase:
                    output.Append(OpeningInLowerCase());
                    if (Tag.RequiresClosing)
                    {
                        pending.Push(new EmitStep(ClosingInLowerCase()));
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

        // DM3 addition against the original: the two tags of a node as typed,
        // with the names in lower case when the grammar read the node as
        // markup. See NodeTree.ToBbInLowerCase.
        //
        // A hiding node the grammar did not accept, or that nobody closed,
        // stays a block on the page and still is no markup the author wrote
        // right: its tags stay as typed, as a broken tag's do.
        private bool IsWrittenRight => !IsBroken && (IsClosed || !Tag.RequiresClosing);

        private string OpeningInLowerCase()
        {
            var opening = OpeningText ?? RebuildOpening(AttributeValue);
            if (!IsWrittenRight || OpeningText == null)
            {
                return opening;
            }

            // What the grammar accepted: "[", the name, then "]", the DM2
            // attribute ="value"], or the parameters name="value" separated by
            // spaces. A value is left as it is: its length is known, since the
            // node holds it, so the scan steps over it whatever it holds.
            var builder = new StringBuilder(opening);
            var index = LowerLetters(builder, 1);
            if (Parameters.Count == 0)
            {
                return builder.ToString();
            }

            while (index < builder.Length && builder[index] == ' ')
            {
                while (index < builder.Length && builder[index] == ' ')
                {
                    index++;
                }

                var nameStart = index;
                index = LowerLetters(builder, index);
                var name = builder.ToString(nameStart, index - nameStart);
                if (!Parameters.TryGetValue(name, out var value))
                {
                    // Not the shape the grammar accepted: leave the rest as it is.
                    return opening;
                }

                // Past =" and the value, onto the closing quote and what
                // follows it: "]" or the space before the next name.
                index += 2 + value.Length + 1;
            }

            return builder.ToString();
        }

        private string ClosingInLowerCase()
        {
            var closing = ClosingText ?? $@"[/{Tag.Name}]";
            if (!IsWrittenRight)
            {
                return closing;
            }

            var builder = new StringBuilder(closing);
            LowerLetters(builder, 2);
            return builder.ToString();
        }

        /// <summary>
        /// Fold A-Z onto a-z in the run of ASCII letters from the index, and
        /// return where the run ends.
        /// </summary>
        private static int LowerLetters(StringBuilder builder, int index)
        {
            while (index < builder.Length)
            {
                var character = builder[index];
                if (character >= 'A' && character <= 'Z')
                {
                    builder[index] = (char)(character + ('a' - 'A'));
                }
                else if (character < 'a' || character > 'z')
                {
                    break;
                }

                index++;
            }

            return index;
        }

        // DM3 addition against the original: which nodes render as the text
        // that was typed.
        //
        // None of these is markup: a broken tag, a tag nobody closed, a content
        // tag whose content is not one value it accepts. Such a node prints its
        // own opening and closing tag as text, and its children render as they
        // would anywhere else. An unclosed tag used to be drawn to the end of the
        // text, and one forgotten [/quote] turned the rest of a post into
        // somebody else's words.
        //
        // A hiding node is the exception, because printing its content is
        // exactly what it exists to prevent: a broken or unclosed one stays a
        // block, and only for the author of the text (Tag.MalformedAsWritten) is
        // it shown as written.
        private bool RendersAsWritten(Func<Node, bool> filter, out string content)
        {
            content = null;
            if (Tag.Hides)
            {
                return Tag.MalformedAsWritten && (IsBroken || !IsClosed);
            }

            if (IsBroken || (Tag.RequiresClosing && !IsClosed))
            {
                return true;
            }

            if (!(Tag is ContentTag contentTag))
            {
                return false;
            }

            content = ReadContent(contentTag, filter);
            return content == null;
        }

        /// <summary>
        /// The value of a closed content tag, or null when the node does not hold
        /// one: holding anything but text, or holding text that is not one value
        /// the tag accepts.
        /// </summary>
        /// <remarks>
        /// DM3 addition against the original: what the filter leaves out is not
        /// held. A block the reader may not read, written inside a link, made the
        /// link the text it was typed for that reader alone, "[link]" and the
        /// address and "[/link]", which told him a block stood there. The reader
        /// gets the link the rest of its content makes, as if the block had not
        /// been written; the reader of the block gets the tag as typed, as before.
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

                if (!(child is TextNode textNode))
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
                case EmitMode.BbInLowerCase:
                    var lower = mode == EmitMode.BbInLowerCase;
                    output.Append(lower ? OpeningInLowerCase() : OpeningText ?? RebuildOpening(AttributeValue));
                    foreach (var child in ChildNodes)
                    {
                        if (filter == null || filter(child))
                        {
                            output.Append(((TextNode)child).Text);
                        }
                    }

                    output.Append(lower ? ClosingInLowerCase() : ClosingText ?? $@"[/{Tag.Name}]");
                    break;
            }
        }

        /// <summary>
        /// The node as it was typed: its opening tag as text, its children in
        /// the mode of its parent, and its own closing tag as text when it had
        /// one.
        /// </summary>
        /// <remarks>
        /// The children keep the parent's mode on purpose: a [noparse] that did
        /// not match the grammar is no statement that its content is an example,
        /// so its content is not shown as source, and a hiding block inside it
        /// is a hiding block. The source of the node is never printed whole,
        /// because a hiding block inside it would be printed with it.
        /// </remarks>
        private void EmitAsWritten(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitStep step,
            Dictionary<string, string> securitySubstitutions,
            Func<Node, bool> filter)
        {
            var opening = OpeningText ?? RebuildOpening(AttributeValue);
            output.Append(Literal(opening, step.Mode, securitySubstitutions));
            if (IsClosed && ClosingText != null)
            {
                pending.Push(new EmitStep(Literal(ClosingText, step.Mode, securitySubstitutions)));
            }

            PushChildren(pending, step.Mode, step.AliasSubstitutions, filter);
        }

        private static string Literal(string text, EmitMode mode, Dictionary<string, string> securitySubstitutions)
        {
            if (mode == EmitMode.Bb || securitySubstitutions == null)
            {
                return text;
            }

            var builder = new StringBuilder(text);
            foreach (var substitution in securitySubstitutions)
            {
                builder.Replace(substitution.Key, substitution.Value);
            }

            return builder.ToString();
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
                // A step and not a write, so that the empty list ends its line
                // as a list with items does (Tag.DropsLineBreakAfter).
                pending.Push(new EmitStep(openHtml + closeHtml, Tag.DropsLineBreakAfter));
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
