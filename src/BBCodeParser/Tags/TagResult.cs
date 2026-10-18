// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System.Collections.Generic;

namespace BBCodeParser.Tags
{
    // DM3 change against the original: one token of the reader. The original
    // carried the text before a tag and the tag itself in one result; the text
    // is its own token now.
    public class TagResult
    {
        public TagType TagType { get; set; }

        /// <summary>The tag an opening or closing token names.</summary>
        public Tag Tag { get; set; }

        /// <summary>The token as it stands in the source.</summary>
        public string Text { get; set; }

        /// <summary>The DM2 attribute of an opening tag, empty when there is none.</summary>
        public string AttributeValue { get; set; }

        /// <summary>The named parameters of an opening tag the grammar accepted.</summary>
        public IReadOnlyDictionary<string, string> Parameters { get; set; }

        /// <summary>Whether the opening tag did not match the grammar.</summary>
        public bool Broken { get; set; }
    }
}
