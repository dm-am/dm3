// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System.Collections.Generic;
using System.Text;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace BBCodeParser
{
    public class BbParser : IBbParser
    {
        private readonly Tag[] tags;
        private readonly Dictionary<string, string> securitySubstitutions;
        private readonly Dictionary<string, string> aliasSubstitutions;
        // DM3 change against the original: 6000 lowered to 512.
        //
        // The guard was written to stop a tree the renderer could not handle,
        // and it was set to twice what the renderer survived: the recursive
        // ToHtml ran out of stack at about 3200 levels and killed the process,
        // so the limit that was supposed to catch that never once fired. The
        // renderer no longer recurses (see Nodes/Node.cs), so nothing here is
        // load-bearing against a crash any more, and the guard goes back to
        // what a guard is for - refusing input that is not text.
        //
        // 512 is chosen against the deepest thing a person writes rather than
        // against a machine limit. A quote chain grows one level per reply and
        // a list nests a handful, so real content lives in the tens; five
        // hundred is two orders of magnitude of headroom and still refuses the
        // 18 KB of nested [b] that used to take the API down. Above it, Parse
        // throws BbParserException, which the render path catches (see
        // BbConverter): the post shows as the text the author typed instead of
        // rendering, and nothing else on the page is affected.
        private const int TreeMaxDepth = 512;

        public static readonly Dictionary<string, string> SecuritySubstitutions = new Dictionary<string, string>
        {
            {"&", "&amp;"},
            {"<", "&lt;"},
            {">", "&gt;"}
        };

        // DM3 addition against the original: what the alias substitutions are in
        // the plain text, where they cannot be markup. See NodeTree.ToText.
        private readonly Dictionary<string, string> textSubstitutions;

        public BbParser(
            Tag[] tags,
            Dictionary<string, string> securitySubstitutions,
            Dictionary<string, string> aliasSubstitutions,
            Dictionary<string, string> textSubstitutions = null)
        {
            this.tags = tags;
            this.securitySubstitutions = securitySubstitutions;
            this.aliasSubstitutions = aliasSubstitutions;
            this.textSubstitutions = textSubstitutions;
        }

        // DM3 addition against the original: what a character cannot be.
        //
        // Control characters and unpaired surrogates travelled through the
        // parser byte for byte and came out of it byte for byte, so every
        // consumer downstream met them and each decided for itself whether to
        // clean them. One already had to: the search projection strips control
        // characters locally because the result preview marks a match with two
        // of them, and one arriving inside a body would be read as a mark. That
        // is one consumer out of however many there will be, and the fix has to
        // be where all of them pass - here, before anything is read.
        //
        // Removed: the C0 and C1 control characters other than tab, line feed
        // and carriage return, which are the three that mean something in a
        // body.
        //
        // Removed as well: the characters that reverse the order in which text
        // is drawn, the embeddings and overrides U+202A..U+202E and the isolates
        // U+2066..U+2069. With one of them a word on the page reads differently
        // from the word that is stored, and in the middle of a tag name it split
        // the name for one reader of the text and not for another. The marks
        // U+200E and U+200F reorder nothing and stay.
        //
        // Replaced with U+FFFD: a surrogate with no partner. On its own it is
        // not a character at all, and every layer below answers for it
        // differently - System.Text.Json substitutes U+FFFD, PostgreSQL refuses
        // the whole string - so it is decided once, here.
        //
        // The save path reads the text after the same removal (ParserInput in
        // DM.Domain.Core, which cannot reference this assembly), because removing
        // a character joins what stood on either side of it, and the join can be
        // a tag. ControlCharacterMarkupShould holds the two to one answer.
        private const char Replacement = '\uFFFD';

        private static bool IsStripped(char character) =>
            (char.IsControl(character) && character != '\t' && character != '\n' && character != '\r') ||
            (character >= '\u202A' && character <= '\u202E') ||
            (character >= '\u2066' && character <= '\u2069');

        /// <summary>
        /// Index of the first character the normalisation would change, or -1
        /// when there is none. Text with nothing wrong in it - which is nearly
        /// all text - is then returned as the very instance that came in.
        /// </summary>
        private static int FindFirstToNormalise(string input)
        {
            for (var i = 0; i < input.Length; i++)
            {
                var character = input[i];
                if (IsStripped(character))
                {
                    return i;
                }

                if (char.IsHighSurrogate(character))
                {
                    if (i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                    {
                        i++;
                        continue;
                    }

                    return i;
                }

                if (char.IsLowSurrogate(character))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string Normalise(string input)
        {
            var first = FindFirstToNormalise(input);
            if (first < 0)
            {
                return input;
            }

            var builder = new StringBuilder(input.Length);
            builder.Append(input, 0, first);
            for (var i = first; i < input.Length; i++)
            {
                var character = input[i];
                if (IsStripped(character))
                {
                    continue;
                }

                if (char.IsHighSurrogate(character))
                {
                    // A pair is one character and stays one character; a high
                    // surrogate that nothing follows is not.
                    if (i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                    {
                        builder.Append(character).Append(input[i + 1]);
                        i++;
                        continue;
                    }

                    builder.Append(Replacement);
                    continue;
                }

                if (char.IsLowSurrogate(character))
                {
                    // Every low surrogate that had a partner was consumed above,
                    // so reaching here means it has none.
                    builder.Append(Replacement);
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        public NodeTree Parse(string input)
        {
            input = string.IsNullOrEmpty(input) ? input : Normalise(input);

            var nodeTree = new NodeTree(securitySubstitutions, aliasSubstitutions, textSubstitutions);
            if (string.IsNullOrWhiteSpace(input))
            {
                nodeTree.AddChild(new TextNode(string.Empty));
                return nodeTree;
            }

            var reader = new Reader(input, tags);
            var current = (Node)nodeTree;
            var treeDepth = 0;

            while (reader.TryRead(out var token))
            {
                switch (token.TagType)
                {
                    case TagType.NoResult:
                        AddText(current, token);
                        break;

                    case TagType.Open:
                        var tagNode = Open(current, token, ref treeDepth);
                        if (!token.Tag.RequiresClosing)
                        {
                            break;
                        }

                        // DM3 change against the original: a block shown as
                        // written is read as one piece, up to its own closing
                        // tag. The original fed every tag found inside it back
                        // as text one match at a time, which is the same thing
                        // for a closed block. Such a block is a CodeTag, and
                        // the catalogue declares one: [noparse] (see CodeTag).
                        //
                        // A block nobody closed is no example of anything: it
                        // stays open like any other tag, what follows it is read
                        // as markup, and it renders as the text it is (see
                        // TagNode). A hiding block written under it is then a
                        // hiding block, as it is anywhere else.
                        if (token.Tag is CodeTag && !token.Broken &&
                            reader.TryReadVerbatim(token.Tag, out var content, out var closer))
                        {
                            AddText(tagNode, content);
                            tagNode.Close(closer);
                            treeDepth--;
                            break;
                        }

                        current = tagNode;
                        break;

                    case TagType.Close:
                        // DM3 change against the original: which node a
                        // closing tag closes.
                        //
                        // The loop that stood here read the name of the open
                        // node once, before it started, and compared that same
                        // captured name on every turn. The comparison could
                        // therefore never become false, so a closing tag that
                        // did not match the open node unwound the whole stack to
                        // the root - and everything written after it left the
                        // block it was written inside.
                        //
                        // For formatting that lost markup nobody asked to lose:
                        // [b][i]x[/i]y[/b] kept y bold only by accident of
                        // order. For [private] it was a leak. Given
                        //
                        //     [private="X"]secret[/b]tail[/private]
                        //
                        // the private node was closed by the stray [/b], "tail"
                        // was added to the root, and the closing [/private]
                        // found nothing left to close: the visibility filter
                        // removed the private node and the page printed "tail"
                        // to everyone.
                        //
                        // The search below stops at the nearest ancestor of that
                        // name, and never crosses a sealed one (see
                        // Tag.SealedByOwnTag). What is opened inside the node it
                        // closes and left open stays unclosed, and renders as
                        // the text it is.
                        //
                        // A closing tag that finds no such ancestor closes
                        // nothing and is printed as it was typed. The original
                        // dropped it, and an author explaining how a tag is
                        // closed lost half the explanation without a word.
                        var target = FindNodeToClose(current, nodeTree, token.Tag.Name);
                        if (target == null)
                        {
                            AddText(current, token);
                            break;
                        }

                        ((TagNode)target).Close(token.Text);
                        while (current != target)
                        {
                            current = current.ParentNode;
                            treeDepth--;
                        }

                        current = current.ParentNode;
                        treeDepth--;
                        break;
                }
            }

            return nodeTree;
        }

        private static void AddText(Node parent, TagResult token)
        {
            if (!string.IsNullOrEmpty(token.Text))
            {
                parent.AddChild(new TextNode(token.Text));
            }
        }

        private static TagNode Open(Node parent, TagResult token, ref int treeDepth)
        {
            var tagNode = new TagNode(token.Tag, parent, token.AttributeValue, token.Parameters, token.Broken,
                token.Text);
            parent.AddChild(tagNode);
            if (token.Tag.RequiresClosing)
            {
                treeDepth++;
                if (treeDepth > TreeMaxDepth)
                {
                    throw new BbParserException();
                }
            }

            return tagNode;
        }

        /// <summary>
        /// The open node a closing tag of the given name ends, or null when it
        /// ends none.
        /// </summary>
        /// <remarks>
        /// Nearest first, so what was opened outside the match stays open. The
        /// walk stops at a sealed node without looking past it: a closing tag is
        /// not allowed to end a block that hides its content, and everything
        /// written between it and that block's own closing tag stays inside.
        /// A sealed node still answers to its own name, because the name is
        /// tested before the seal is.
        /// </remarks>
        private static Node FindNodeToClose(Node current, Node root, string tagName)
        {
            for (var node = current; node != null && node != root; node = node.ParentNode)
            {
                if (!(node is TagNode tagNode))
                {
                    return null;
                }

                if (tagNode.Tag.Name == tagName)
                {
                    return node;
                }

                if (tagNode.Tag.SealedByOwnTag)
                {
                    return null;
                }
            }

            return null;
        }

        public Tag[] GetTags()
        {
            return tags;
        }
    }
}
