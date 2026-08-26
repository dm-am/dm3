// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System.Linq;
using System.Text.RegularExpressions;

namespace BBCodeParser.Tags
{
    public class Tag
    {
        private static readonly Regex JsXssSecureRegex = new Regex("(javascript|data):", RegexOptions.IgnoreCase);

        private static readonly Regex[] EscapeRegexes =
        {
            new Regex("\"|'|`|\\n|\\s|\\t|\\r", RegexOptions.IgnoreCase),
            new Regex("&#[\\d\\w]+;?", RegexOptions.IgnoreCase)
        };

        private string OpenTag { get; }
        private string CloseTag { get; }
        public bool WithAttribute { get; }
        public bool RequiresClosing { get; }
        private bool Secure { get; }
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

        public Tag(string name, string openTag, string closeTag, bool withAttribute = false, bool secure = true,
            bool sealedByOwnTag = false)
        {
            OpenTag = openTag;
            CloseTag = closeTag;
            WithAttribute = withAttribute;
            Secure = secure;
            RequiresClosing = true;
            Name = name;
            SealedByOwnTag = sealedByOwnTag;
        }

        public Tag(string name, string openTag, bool withAttribute = false, bool secure = true)
        {
            OpenTag = openTag;
            CloseTag = null;
            WithAttribute = withAttribute;
            Secure = secure;
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

        private string GetHtmlPart(string tagPart, string attributeValue)
        {
            return WithAttribute ? tagPart.Replace("{value}", GetAttributeValueForHtml(attributeValue)) : tagPart;
        }

        private string GetAttributeValueForHtml(string attributeValue)
        {
            // DM3 change against the original: the second test of Secure is
            // gone. It stood after the early return above, so its false branch
            // was unreachable and read as a choice that was never made.
            // Nothing about what this method does has changed.
            if (!Secure)
            {
                return attributeValue;
            }

            return JsXssSecureRegex.Replace(EscapeSpecialCharacters(attributeValue), "_xss_");
        }

        private static string EscapeSpecialCharacters(string value)
        {
            while (true)
            {
                var escaped = EscapeRegexes.Aggregate(value, (input, regex) => regex.Replace(input, string.Empty));
                if (escaped == value)
                {
                    return escaped;
                }
                value = escaped;
            }
        }
    }
}
