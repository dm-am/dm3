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

        public NodeTree(
            Dictionary<string, string> securitySubstitutions,
            Dictionary<string, string> aliasSubstitutions
        )
        {
            this.securitySubstitutions = securitySubstitutions;
            this.aliasSubstitutions = aliasSubstitutions;
            ChildNodes = new List<Node>();
            ParentNode = null;
        }

        // DM3 change against the original: the public entry points below are
        // unchanged in signature and in output, but each now starts one
        // iterative walk instead of a recursive descent. See Node.cs.

        public string ToHtml(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            return Render(this, EmitMode.Html, securitySubstitutions, aliasSubstitutions, filter,
                filterAttributeValue);
        }

        public string ToText(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            return Render(this, EmitMode.Text, securitySubstitutions, aliasSubstitutions, filter,
                filterAttributeValue);
        }

        public string ToBb(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            // Security substitutions stay off on this path, as in the original:
            // the point is to give back the source the author typed.
            return Render(this, EmitMode.Bb, null, null, filter, filterAttributeValue);
        }

        internal override void Emit(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitMode mode,
            Dictionary<string, string> securitySubstitutions,
            Dictionary<string, string> aliasSubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue)
        {
            PushChildren(pending, mode, aliasSubstitutions, filter);
        }
    }
}
