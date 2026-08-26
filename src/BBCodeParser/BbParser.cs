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

        public BbParser(
            Tag[] tags,
            Dictionary<string, string> securitySubstitutions,
            Dictionary<string, string> aliasSubstitutions)
        {
            this.tags = tags;
            this.securitySubstitutions = securitySubstitutions;
            this.aliasSubstitutions = aliasSubstitutions;
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
        // be where all of them pass - here. Not in the wrapper around this
        // class: the wrapper rewrites its input before handing it over, so a
        // rule placed there is a rule about the wrapper's text rather than
        // about the text this parser reads.
        //
        // Removed: the C0 and C1 control characters other than tab, line feed
        // and carriage return, which are the three that mean something in a
        // body.
        //
        // Replaced with U+FFFD: a surrogate with no partner. On its own it is
        // not a character at all, and every layer below answers for it
        // differently - System.Text.Json substitutes U+FFFD, PostgreSQL refuses
        // the whole string - so it is decided once, here.
        //
        // Deliberately untouched: the characters that reorder text for display,
        // U+202A..U+202E and the isolates near them. Stripping them changes what
        // a reader sees, which is a product decision and not one this class gets
        // to make on its own.
        private const char Replacement = '\uFFFD';

        private static bool IsStrippedControl(char character) =>
            char.IsControl(character) && character != '\t' && character != '\n' && character != '\r';

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
                if (IsStrippedControl(character))
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
                if (IsStrippedControl(character))
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

            var nodeTree = new NodeTree(securitySubstitutions, aliasSubstitutions);
            var treeDepth = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                nodeTree.AddChild(new TextNode(string.Empty));
                return nodeTree;
            }

            var reader = new Reader(input, tags);
            var current = (Node)nodeTree;
            if (!reader.TryRead(out var tagResult))
            {
                current.AddChild(new TextNode(input));
            }
            else
            {
                do
                {
                    if (!string.IsNullOrEmpty(tagResult.Text))
                    {
                        current.AddChild(new TextNode(tagResult.Text));
                    }

                    // no parsing inside CodeTag
                    var isInsideCodeTag = (current as TagNode)?.Tag is CodeTag;
                    var resultIsClosingCodeTag = tagResult.Tag is CodeTag && tagResult.TagType == TagType.Close;
                    if (isInsideCodeTag && !resultIsClosingCodeTag && !string.IsNullOrEmpty(tagResult.Match) &&
                        tagResult.Tag != null)
                    {
                        current.AddChild(new TextNode(tagResult.Match));
                        continue;
                    }

                    switch (tagResult.TagType)
                    {
                        case TagType.NoResult:
                            continue;
                        case TagType.Open:
                            var tagNode = new TagNode(tagResult.Tag, current, tagResult.AttributeValue);
                            current.AddChild(tagNode);

                            if (!tagResult.Tag.RequiresClosing) continue;

                            current = tagNode;
                            treeDepth++;
                            if (treeDepth > TreeMaxDepth)
                            {
                                throw new BbParserException();
                            }

                            break;
                        default:
                            if (tagResult.TagType == TagType.Close && current != nodeTree)
                            {
                                // DM3 change against the original: which node a
                                // closing tag closes.
                                //
                                // The loop that stood here read the name of the
                                // open node once, before it started, and
                                // compared that same captured name on every
                                // turn. The comparison could therefore never
                                // become false, so a closing tag that did not
                                // match the open node unwound the whole stack to
                                // the root - and everything written after it
                                // left the block it was written inside.
                                //
                                // For formatting that lost markup nobody asked
                                // to lose: [b][i]x[/i]y[/b] kept y bold only by
                                // accident of order. For [private] it was a
                                // leak. Given
                                //
                                //     [private="X"]secret[/b]tail[/private]
                                //
                                // the private node was closed by the stray [/b],
                                // "tail" was added to the root, and the closing
                                // [/private] found nothing left to close: the
                                // visibility filter removed the private node and
                                // the page printed "tail" to everyone. None of
                                // the three checks around it could see that -
                                // the save-time balance check counts [private]
                                // tags and finds one of each, the normaliser
                                // rewrites spellings rather than structure, and
                                // the depth guard measures depth.
                                //
                                // The search below stops at the nearest ancestor
                                // of that name, and never crosses a sealed one
                                // (see Tag.SealedByOwnTag). A closing tag that
                                // finds no such ancestor closes nothing, which
                                // is what it already did when the stack held
                                // nothing of that name at all.
                                var target = FindNodeToClose(current, nodeTree, tagResult.Tag.Name);
                                while (target != null && current != target)
                                {
                                    current = current.ParentNode;
                                    treeDepth--;
                                }

                                if (target != null)
                                {
                                    current = current.ParentNode;
                                    treeDepth--;
                                }
                            }

                            break;
                    }
                } while (reader.TryRead(out tagResult));
            }

            return nodeTree;
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
