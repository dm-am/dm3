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
        /// DM3 addition against the original: public, so a filter can read the
        /// address a content tag holds before the walk decides to draw it.
        /// </remarks>
        public string Text => text;

        /// <summary>
        /// This text less one leading CR LF or LF, for the text right after a block
        /// that ends its line (Tag.DropsLineBreakAfter).
        /// </summary>
        internal TextNode WithoutLeadingLineBreak()
        {
            if (text.StartsWith("\r\n", StringComparison.Ordinal))
            {
                return new TextNode(text.Substring(2));
            }

            return text.StartsWith("\n", StringComparison.Ordinal) ? new TextNode(text.Substring(1)) : this;
        }

        internal static string SubstituteText(string text, Dictionary<string, string> substitutions)
        {
            return substitutions == null
                ? text
                : substitutions.Aggregate(text,
                    (current, substitution) => current.Replace(substitution.Key, substitution.Value));
        }

        // DM3 change against the original: the three renderers folded into one step.
        internal override void Emit(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitStep step,
            Dictionary<string, string> securitySubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue)
        {
            if (step.Mode == EmitMode.Bb)
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
