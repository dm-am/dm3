// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BBCodeParser.Nodes
{
    public class TextNode : Node
    {
        private readonly string text;

        public TextNode(string text)
        {
            this.text = text;
        }

        /// <summary>
        /// The text as the author typed it, before any substitution.
        /// </summary>
        /// <remarks>
        /// Read by TagNode.EmitList, which has to tell whitespace between list
        /// items from words between them and cannot do it after substitution:
        /// by then a newline has already become a line break. Read by a content
        /// tag as well, whose value is the text as typed.
        /// </remarks>
        internal string Text => text;

        /// <summary>
        /// This text less one line break at its start, CR LF or LF, for the text
        /// that stands straight after a block that ends its line (see
        /// Tag.DropsLineBreakAfter). A CR alone is not a line break on the page
        /// (see the substitutions of BbParserProvider) and stays.
        /// </summary>
        internal TextNode WithoutLeadingLineBreak()
        {
            if (text.StartsWith("\r\n", StringComparison.Ordinal))
            {
                return new TextNode(text.Substring(2));
            }

            return text.StartsWith("\n", StringComparison.Ordinal) ? new TextNode(text.Substring(1)) : this;
        }

        private static string SubstituteText(string text, Dictionary<string, string> substitutions)
        {
            return substitutions == null
                ? text
                : substitutions.Aggregate(text,
                    (current, substitution) => current.Replace(substitution.Key, substitution.Value));
        }

        // DM3 change against the original: ToHtml, ToText and ToBb are folded
        // into this one step. See Node.cs. The substitutions applied are the ones
        // the walk hands in - both tables for HTML, the text table alone for
        // text (see NodeTree.ToText), security only for BBCode source.
        internal override void Emit(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitStep step,
            Dictionary<string, string> securitySubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue)
        {
            if (step.Mode == EmitMode.Bb || step.Mode == EmitMode.BbInLowerCase)
            {
                output.Append(SubstituteText(text, securitySubstitutions));
                return;
            }

            output.Append(SubstituteText(SubstituteText(text, securitySubstitutions), step.AliasSubstitutions));
        }

        public override void AddChild(Node node)
        {
            throw new NotImplementedException();
        }
    }
}
