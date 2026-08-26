// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.
//
// DM3 change against the original: this file is new. The original had three
// parallel renderers (ToHtml / ToText / ToBb), each overridden once per node
// type and each recursing into its children; this enum is what lets the single
// iterative walk in Node.Render carry "which of the three outputs am I
// producing" as data instead of as a call target. See Node.cs for why the
// recursion had to go.

namespace BBCodeParser.Nodes
{
    /// <summary>Which of the three outputs one walk of the tree produces.</summary>
    internal enum EmitMode
    {
        /// <summary>Rendered HTML, security and alias substitutions applied.</summary>
        Html,

        /// <summary>Text with no tag markup, substitutions still applied.</summary>
        Text,

        /// <summary>BBCode source rebuilt from the tree.</summary>
        Bb
    }
}
