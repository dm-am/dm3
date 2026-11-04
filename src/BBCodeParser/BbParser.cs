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
        // DM3 change against the original: 6000 lowered to 512. Written text nests
        // in the tens; deeper input is refused with BbParserException.
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

        // DM3 addition against the original: control characters, the bidi
        // overrides and unpaired surrogates are cleaned here, where every consumer
        // passes. The save path cleans the same way (ParserInput in DM.Domain.Core).
        private const char Replacement = '\uFFFD';

        private static bool IsStripped(char character) =>
            (char.IsControl(character) && character != '\t' && character != '\n' && character != '\r') ||
            (character >= '\u202A' && character <= '\u202E') ||
            (character >= '\u2066' && character <= '\u2069');

        /// <summary>
        /// Index of the first character the normalisation would change, or -1, so
        /// that clean text is returned as the same instance.
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
                    // A low surrogate with a partner was consumed above.
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

                        // DM3 change against the original: a closed block shown
                        // as written is read as one piece; an unclosed one stays
                        // open like any other tag.
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
                        // DM3 change against the original: a closing tag closes
                        // the nearest open ancestor of its name, never across a
                        // sealed one, and with none it is printed as typed.
                        var target = FindNodeToClose(current, token.Tag.Name);
                        if (target == null)
                        {
                            AddText(current, token);
                            break;
                        }

                        target.Close(token.Text);
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
        /// The open node a closing tag of the given name ends, or null. A sealed
        /// node stops the walk but still answers to its own name.
        /// </summary>
        private static TagNode FindNodeToClose(Node current, string tagName)
        {
            for (var node = current as TagNode; node != null; node = node.ParentNode as TagNode)
            {
                if (node.Tag.Name == tagName)
                {
                    return node;
                }

                if (node.Tag.SealedByOwnTag)
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
