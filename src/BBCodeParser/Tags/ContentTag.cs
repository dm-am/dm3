// DM3 addition to the vendored BBCodeParser: this file is new.

using System.Collections.Generic;
using BBCodeParser.Nodes;

namespace BBCodeParser.Tags
{
    /// <summary>
    /// A tag whose content is one value rather than text: an address, a login.
    /// </summary>
    /// <remarks>
    /// A node like any other, so a hiding block inside it is a hiding block. A closed
    /// node holding one accepted value renders it; otherwise it renders as typed.
    /// </remarks>
    public abstract class ContentTag : Tag
    {
        protected ContentTag(string name, IReadOnlyDictionary<string, ParameterKind> parameters)
            : base(name, string.Empty, string.Empty)
        {
            Parameters = parameters;
        }

        /// <summary>
        /// The value the raw text between the two tags stands for, or null when
        /// it is not one value of this tag's kind or is one the tag refuses to
        /// render (an address it will not put on a page). Null renders the node
        /// as it was written.
        /// </summary>
        public abstract string ReadContent(string raw);

        /// <summary>The whole markup of an accepted node.</summary>
        public abstract string RenderHtml(TagNode node, string content);

        /// <summary>The text an accepted node contributes to the plain-text output.</summary>
        public abstract string RenderText(TagNode node, string content);
    }
}
