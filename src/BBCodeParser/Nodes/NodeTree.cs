// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System;
using System.Collections.Generic;
using System.Text;

namespace BBCodeParser.Nodes
{
    public class NodeTree : Node
    {
        private readonly Dictionary<string, string> securitySubstitutions;
        private readonly Dictionary<string, string> aliasSubstitutions;
        private readonly Dictionary<string, string> textSubstitutions;

        public NodeTree(
            Dictionary<string, string> securitySubstitutions,
            Dictionary<string, string> aliasSubstitutions,
            Dictionary<string, string> textSubstitutions = null
        )
        {
            this.securitySubstitutions = securitySubstitutions;
            this.aliasSubstitutions = aliasSubstitutions;
            this.textSubstitutions = textSubstitutions;
            ChildNodes = new List<Node>();
            ParentNode = null;
        }

        // DM3 change against the original: each entry point starts the one iterative walk of Node.Render.
        public string ToHtml(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            return Render(this, EmitMode.Html, securitySubstitutions, aliasSubstitutions, filter, filterAttributeValue);
        }

        // DM3 change against the original: no HTML escaping and the text
        // substitutions, so a reader of plain text gets no entities to undo.
        public string ToText(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            return Render(this, EmitMode.Text, null, textSubstitutions, filter, filterAttributeValue);
        }

        public string ToBb(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            // Security substitutions stay off: this gives back the source as typed.
            return Render(this, EmitMode.Bb, null, null, filter, filterAttributeValue);
        }

        internal override void Emit(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitStep step,
            Dictionary<string, string> securitySubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue)
        {
            PushChildren(pending, step.Mode, step.AliasSubstitutions, filter);
        }
    }
}
