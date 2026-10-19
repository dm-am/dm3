// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.
//
// DM3 change against the original: this file is new, the output of the one walk in Node.Render.

namespace BBCodeParser.Nodes
{
    /// <summary>Which of the three outputs one walk of the tree produces.</summary>
    internal enum EmitMode
    {
        /// <summary>Rendered HTML, security and alias substitutions applied.</summary>
        Html,

        /// <summary>Plain text: no markup and no entities, the text substitutions applied.</summary>
        Text,

        /// <summary>BBCode source rebuilt from the tree.</summary>
        Bb
    }
}
