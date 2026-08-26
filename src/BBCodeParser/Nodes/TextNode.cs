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
        /// by then a newline has already become a line break.
        /// </remarks>
        internal string Text => text;

        private static string SubstituteText(string text, Dictionary<string, string> substitutions)
        {
            return substitutions == null
                ? text
                : substitutions.Aggregate(text,
                    (current, substitution) => current.Replace(substitution.Key, substitution.Value));
        }

        // DM3 change against the original: ToHtml, ToText and ToBb are folded
        // into this one step. See Node.cs. The substitutions applied are the
        // original ones - both tables for HTML and for text, security only for
        // BBCode source.
        internal override void Emit(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitMode mode,
            Dictionary<string, string> securitySubstitutions,
            Dictionary<string, string> aliasSubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue)
        {
            output.Append(mode == EmitMode.Bb
                ? SubstituteText(text, securitySubstitutions)
                : SubstituteText(SubstituteText(text, securitySubstitutions), aliasSubstitutions));
        }

        public override void AddChild(Node node)
        {
            throw new NotImplementedException();
        }
    }
}
