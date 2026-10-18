// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System.Collections.Generic;
using System.Net;
using BBCodeParser.Nodes;

namespace BBCodeParser.Tags
{
    public class Tag
    {
        private static readonly IReadOnlyDictionary<string, ParameterKind> NoParameters =
            new Dictionary<string, ParameterKind>();

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

        // DM3 addition against the original: a tag that only its own closing
        // tag ends.
        //
        // A closing tag whose name belongs to an ancestor closes that ancestor
        // and, with it, everything opened inside - which for an ordinary
        // formatting tag is what the author meant, and for a block that hides
        // its content is a hole. Written
        //
        //     [b]bold [private="X"]secret[/b] and more[/private]
        //
        // the [/b] would end the private block early, and "and more" - written
        // by its author inside that block - would be printed to the whole room.
        // The stray tag needs no malice: opening a formatting tag before a block
        // and closing it inside is the most ordinary nesting mistake there is.
        //
        // So a sealed tag stops the search: a closing tag that would have to
        // cross one closes nothing at all. The block keeps its content and ends
        // where its own closing tag says, and the direction of the refusal is
        // the one the rest of this codebase takes for the same question - text
        // withheld from a reader who should have seen it is a complaint, text
        // shown to a reader who should not have is not recoverable.
        public bool SealedByOwnTag { get; }

        // DM3 addition against the original: the named parameters of the tag,
        // written after the name as name="value" pairs separated by spaces. A
        // tag declares the names it reads and the shape of each value; anything
        // else written in the opening tag makes it a broken tag, which renders as
        // the text it is (see Reader).
        public IReadOnlyDictionary<string, ParameterKind> Parameters { get; init; } = NoParameters;

        // DM3 addition against the original: a tag whose content some readers
        // must not see.
        //
        // Its opening token is recognised whatever follows the name, and a
        // spelling the grammar does not accept still becomes a node: a tag the
        // parser refused would hand its content to its parent, and for a hiding
        // block that is its secret printed to everyone. Such a node carries no
        // attribute and is marked broken, and it is the visibility filter's
        // business what a reader sees of it. The same holds for a block nobody
        // closed, which every other tag prints as the text it is and a hiding
        // one does not (see TagNode).
        public bool Hides { get; init; }

        // DM3 addition against the original: a hiding tag rendered for the author
        // of the text, who reads everything anyway. A broken or unclosed block is
        // then shown as the text that was typed, so the editor is given back the
        // spelling the save path refuses rather than a corrected one nobody wrote.
        public bool MalformedAsWritten { get; init; }

        // DM3 addition against the original: a tag drawn as a block, whose
        // element ends the line it stands on.
        //
        // One line break typed straight after its closing tag would end that
        // line a second time: the <br /> stands alone on a line under the block,
        // and the page drew an empty line between the block and the text under
        // it. The break typed straight before a block draws none, the browser
        // drawing no line for a break that ends the text in front of a block, so
        // the author saw an empty line under every block and none over it. The
        // HTML path leaves that one break out (see Node.Render); a second one is
        // an empty line the author asked for, and is drawn.
        //
        // The tags of the author's editor do not carry it: the editor is given
        // every line break back as typed. Nor does the plain text, where a block
        // ends no line and the break typed after it is the only one there is.
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
            // A tag that needs no closing tag is never on the stack this
            // protects, so there is nothing for it to seal.
            SealedByOwnTag = false;
        }

        public string GetOpenHtml(string attributeValue)
        {
            return GetHtmlPart(OpenTag, attributeValue);
        }

        public string GetCloseHtml(string attributeValue)
        {
            return GetHtmlPart(CloseTag, attributeValue);
        }

        // DM3 addition against the original: the markup of one node, for a tag
        // whose markup depends on more than the attribute - the named parameters
        // the node was written with. The default is the template.
        public virtual string GetOpenHtml(TagNode node, string attributeValue)
        {
            return GetOpenHtml(attributeValue);
        }

        public virtual string GetCloseHtml(TagNode node, string attributeValue)
        {
            return GetCloseHtml(attributeValue);
        }

        // DM3 change against the original: the value is HTML-encoded where it is
        // written, and nowhere else.
        //
        // The original either substituted it as it stood (secure: false) or
        // deleted spaces, quotes and "javascript:" from it (secure: true). The
        // first put author text into markup unescaped; the second mangled an
        // ordinary name - "A & B" became "A&B" - and still let <svg/onload=...>
        // through, where no space is needed. Neither was an escape. Every
        // template writes the value either as element text or inside a quoted
        // attribute, and encoding covers both; the browser decodes it back for
        // display and for getAttribute, so the reader sees what was typed.
        private string GetHtmlPart(string tagPart, string attributeValue)
        {
            return WithAttribute
                ? tagPart.Replace("{value}", WebUtility.HtmlEncode(attributeValue ?? string.Empty))
                : tagPart;
        }
    }
}
