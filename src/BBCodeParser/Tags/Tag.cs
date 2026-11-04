// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using BBCodeParser.Nodes;

namespace BBCodeParser.Tags
{
    public class Tag
    {
        private string OpenTag { get; }
        private string CloseTag { get; }

        /// <summary>
        /// Whether the tag takes the DM2 attribute, written straight after the
        /// name as <c>="value"</c>. The value runs to the first quote followed by
        /// the closing bracket and may carry neither an opening bracket nor a line
        /// break.
        /// </summary>
        public bool WithAttribute { get; }

        public bool RequiresClosing { get; }
        public string Name { get; }

        // DM3 addition against the original: a tag only its own closing tag ends,
        // so a stray [/b] inside a hiding block cannot print the rest of it.
        public bool SealedByOwnTag { get; }

        // DM3 addition against the original: the named parameters the tag reads;
        // anything else in the opening tag makes it broken.
        public IReadOnlyDictionary<string, ParameterKind> Parameters { get; init; } =
            ReadOnlyDictionary<string, ParameterKind>.Empty;

        // DM3 addition against the original: a tag whose content some readers must
        // not see. Any spelling of it becomes a node, so its content never falls to
        // the parent; the visibility filter decides who reads it.
        public bool Hides { get; init; }

        // DM3 addition against the original: a block whose element ends its line,
        // so the HTML path drops one line break typed right after it (Node.Render).
        public bool DropsLineBreakAfter { get; init; }

        public Tag(string name, string openTag, string closeTag, bool withAttribute = false,
            bool sealedByOwnTag = false)
        {
            OpenTag = openTag;
            CloseTag = closeTag;
            WithAttribute = withAttribute;
            RequiresClosing = true;
            Name = name;
            SealedByOwnTag = sealedByOwnTag;
        }

        public Tag(string name, string openTag, bool withAttribute = false)
        {
            OpenTag = openTag;
            CloseTag = null;
            WithAttribute = withAttribute;
            RequiresClosing = false;
            Name = name;
        }

        public string GetOpenHtml(string attributeValue)
        {
            return GetHtmlPart(OpenTag, attributeValue);
        }

        public string GetCloseHtml(string attributeValue)
        {
            return GetHtmlPart(CloseTag, attributeValue);
        }

        // DM3 addition against the original: the markup of one node, for a tag whose
        // markup depends on its parameters. The default is the template.
        public virtual string GetOpenHtml(TagNode node, string attributeValue)
        {
            return GetOpenHtml(attributeValue);
        }

        public virtual string GetCloseHtml(TagNode node, string attributeValue)
        {
            return GetCloseHtml(attributeValue);
        }

        // DM3 change against the original: the value is HTML-encoded where it is
        // written, instead of stripped of characters, which escaped nothing.
        private string GetHtmlPart(string tagPart, string attributeValue)
        {
            return WithAttribute
                ? tagPart.Replace("{value}", WebUtility.HtmlEncode(attributeValue ?? string.Empty))
                : tagPart;
        }
    }
}
