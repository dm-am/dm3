// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.
//
// DM3 change against the original: this file is new. It is one entry of the
// explicit stack that replaced the call stack in Node.Render - see Node.cs.

using System.Collections.Generic;

namespace BBCodeParser.Nodes
{
    /// <summary>
    /// One item of work on the emit stack: either a node still to be emitted,
    /// or a piece of output already decided on and waiting for that node's
    /// children to be written first.
    /// </summary>
    /// <remarks>
    /// The literal form is what carries a closing tag. A recursive renderer
    /// keeps it in a local variable while it descends; an iterative one has
    /// nowhere to keep it, so it goes on the same stack under the children and
    /// comes back off after them.
    ///
    /// The alias substitutions travel per step rather than per walk because a
    /// preformatted tag turns them off for its whole subtree, and the mode
    /// travels per step because a code tag switches its subtree to BBCode
    /// output. Both used to be expressed by what the recursive call passed
    /// down.
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
        }

        internal EmitStep(string literal)
        {
            Node = null;
            Literal = literal;
            Mode = EmitMode.Html;
            AliasSubstitutions = null;
        }
    }
}
