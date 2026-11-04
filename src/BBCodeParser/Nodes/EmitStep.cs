// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.
//
// DM3 change against the original: this file is new, an entry of the stack of Node.Render.

using System.Collections.Generic;

namespace BBCodeParser.Nodes
{
    /// <summary>
    /// One item of work on the emit stack: either a node still to be emitted,
    /// or a piece of output already decided on and waiting for that node's
    /// children to be written first.
    /// </summary>
    /// <remarks>
    /// The mode and the substitutions travel per step because a tag can switch
    /// them for its subtree.
    /// </remarks>
    internal readonly struct EmitStep
    {
        /// <summary>Node to emit, or null when this step is a literal.</summary>
        internal Node Node { get; }

        /// <summary>Output to append verbatim, or null when this step is a node.</summary>
        internal string Literal { get; }

        /// <summary>Output the node is emitted into.</summary>
        internal EmitMode Mode { get; }

        /// <summary>Alias substitutions in force for this node, possibly null.</summary>
        internal Dictionary<string, string> AliasSubstitutions { get; }

        internal EmitStep(Node node, EmitMode mode, Dictionary<string, string> aliasSubstitutions)
        {
            Node = node;
            Literal = null;
            Mode = mode;
            AliasSubstitutions = aliasSubstitutions;
            EndsLine = false;
        }

        /// <summary>
        /// Whether this literal closes a block that ends its line, so that one
        /// line break at the start of the text right after it is not drawn (see
        /// Tag.DropsLineBreakAfter).
        /// </summary>
        internal bool EndsLine { get; }

        internal EmitStep(string literal, bool endsLine = false)
        {
            Node = null;
            Literal = literal;
            Mode = EmitMode.Html;
            AliasSubstitutions = null;
            EndsLine = endsLine;
        }
    }
}
