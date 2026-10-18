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

        // DM3 change against the original: the public entry points below are
        // unchanged in signature, but each now starts one iterative walk instead
        // of a recursive descent. See Node.cs.

        public string ToHtml(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            return Render(this, EmitMode.Html, securitySubstitutions, aliasSubstitutions, filter, filterAttributeValue);
        }

        // DM3 change against the original: the text is text. The original ran the
        // text through the substitutions of the HTML, so the plain text carried
        // "&lt;" for "<", "<br />" for a line break and "&ndash;" for a dash, and
        // every reader of it had to know which entities to take back - the search
        // projection did, and a mail would not have. Nothing here is escaped now,
        // a line break stays a line break, and the alias substitutions are the
        // parser's text ones, which spell a dash as the character. Whoever puts
        // this text into markup escapes it there.
        public string ToText(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            return Render(this, EmitMode.Text, null, textSubstitutions, filter, filterAttributeValue);
        }

        public string ToBb(Func<Node, bool> filter = null, Func<Node, string, string> filterAttributeValue = null)
        {
            // Security substitutions stay off on this path, as in the original:
            // the point is to give back the source the author typed.
            return Render(this, EmitMode.Bb, null, null, filter, filterAttributeValue);
        }

        // DM3 addition against the original: the source the author typed, with
        // the name of every tag the grammar read as markup, and the names of
        // its parameters, in lower case. A name is written in lower case
        // everywhere the site writes one, and the save path writes the author's
        // text that way too.
        //
        // Only what is markup changes. A tag printed as the text it was typed
        // (broken, left open, a closing tag with nothing to close, a content
        // tag that holds no value it accepts) is text, and text is not touched;
        // neither is anything inside a closed [noparse], nor any value, the DM2
        // attribute of [private] included. The case of a name means nothing to
        // the grammar, so the page shows the same text either way.
        public string ToBbInLowerCase()
        {
            return Render(this, EmitMode.BbInLowerCase, null, null, null, null);
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
