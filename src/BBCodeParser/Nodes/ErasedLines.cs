// DM3 addition to the vendored BBCodeParser: this file is new.

using System;
using System.Collections.Generic;
using System.Text;

namespace BBCodeParser.Nodes
{
    /// <summary>
    /// A line that held nothing but what the walk erases goes with it, and takes
    /// one line break along.
    /// </summary>
    /// <remarks>
    /// The filter erases a block its reader may not read without a trace (see
    /// Visibility), and a block the author put on a line of its own used to leave
    /// that line behind, empty:
    /// <c>captain.\n[private]...[/private]\nThen</c> gave the reader two breaks
    /// where the text without the block has one, and the empty line said that
    /// something had stood there. So the siblings are read as lines - the text
    /// between two line breaks, CR LF or LF - and a line made of nothing but
    /// erased nodes is dropped together with one of the breaks around it, which is
    /// what the text reads like had the author never written the block. A line
    /// with anything else on it stays as it stands, a space included: the author
    /// typed that space, and it is on the line without the block too.
    ///
    /// The text of the whole input begins and ends lines, so a block at its very
    /// start or end, next to a line break, is on a line of its own as well. A tag
    /// does not: [b][private]x[/private]\nmore[/b] can stand in the middle of a
    /// line of text, and only a line between two breaks inside a tag is known to
    /// be one - save the last line of a tag nobody closed, which runs to the end
    /// of the text and ends where the text does.
    ///
    /// Which break goes is decided so that the lines left keep one break between
    /// each two of them: the one before a dropped line when it is still there,
    /// else the one after. A text node left with nothing is not emitted at all,
    /// so that it cannot take the place of the text a block that ends its line
    /// strips (Tag.DropsLineBreakAfter).
    ///
    /// And a whole text left with nothing but white space once its blocks are
    /// erased is drawn as nothing, the way the parser reads a text that is white
    /// space alone: blank lines around a block the reader does not read would
    /// otherwise be all that is on the page, and say the same thing.
    /// </remarks>
    internal static class ErasedLines
    {
        /// <summary>
        /// The text children of <paramref name="parent" /> that lose a line break,
        /// by index, with what is left of each - null for one left with nothing -
        /// or null when no line goes. <paramref name="shown" /> says, child by
        /// child, whether the walk draws it.
        /// </summary>
        internal static Dictionary<int, TextNode> Trim(Node parent, bool[] shown)
        {
            if (Array.TrueForAll(shown, isShown => isShown))
            {
                return null;
            }

            var children = parent.Children;
            var atTextStart = parent is NodeTree;
            var atTextEnd = parent.ReachesTextEnd();

            // A text of nothing but white space is read as no text at all (see
            // BbParser.Parse), and so is a text left with nothing else once the
            // blocks are gone: its line breaks and spaces are not drawn either.
            if (atTextStart && atTextEnd && IsOnlyWhiteSpaceAround(children, shown))
            {
                var nothing = new Dictionary<int, TextNode>();
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i] is TextNode)
                    {
                        nothing[i] = null;
                    }
                }

                return nothing;
            }

            // The line breaks, as (child, index, length), and for each line whether
            // it holds anything that is drawn and whether it holds something erased.
            var breaks = new List<(int Child, int At, int Length)>();
            var content = new List<bool> { false };
            var erased = new List<bool> { false };

            for (var i = 0; i < children.Count; i++)
            {
                if (!(children[i] is TextNode text))
                {
                    if (shown[i])
                    {
                        content[content.Count - 1] = true;
                    }
                    else
                    {
                        erased[erased.Count - 1] = true;
                    }

                    continue;
                }

                var value = text.Text;
                for (var at = 0; at < value.Length; at++)
                {
                    var length = value[at] == '\n'
                        ? 1
                        : value[at] == '\r' && at + 1 < value.Length && value[at + 1] == '\n'
                            ? 2
                            : 0;
                    if (length == 0)
                    {
                        content[content.Count - 1] = true;
                        continue;
                    }

                    breaks.Add((i, at, length));
                    content.Add(false);
                    erased.Add(false);
                    at += length - 1;
                }
            }

            var lines = content.Count;
            var dropped = new bool[breaks.Count];
            var any = false;
            for (var line = 0; line < lines; line++)
            {
                if (content[line] || !erased[line] ||
                    (line == 0 && !atTextStart) || (line == lines - 1 && !atTextEnd))
                {
                    continue;
                }

                // The break before the line, else the one after it: never both,
                // and never one another dropped line has taken already.
                var before = line - 1;
                var after = line;
                if (before >= 0 && !dropped[before])
                {
                    dropped[before] = true;
                    any = true;
                }
                else if (after < breaks.Count && !dropped[after])
                {
                    dropped[after] = true;
                    any = true;
                }
            }

            if (!any)
            {
                return null;
            }

            var trimmed = new Dictionary<int, TextNode>();
            var builders = new Dictionary<int, (StringBuilder Text, int From)>();
            for (var b = 0; b < breaks.Count; b++)
            {
                if (!dropped[b])
                {
                    continue;
                }

                var (child, at, length) = breaks[b];
                var value = ((TextNode)children[child]).Text;
                if (!builders.TryGetValue(child, out var builder))
                {
                    builder = (new StringBuilder(value.Length), 0);
                }

                builder.Text.Append(value, builder.From, at - builder.From);
                builders[child] = (builder.Text, at + length);
            }

            foreach (var entry in builders)
            {
                var value = ((TextNode)children[entry.Key]).Text;
                var (text, from) = entry.Value;
                text.Append(value, from, value.Length - from);
                trimmed[entry.Key] = text.Length == 0 ? null : new TextNode(text.ToString());
            }

            return trimmed;
        }

        /// <summary>Whether everything the walk shows of the children is white space.</summary>
        private static bool IsOnlyWhiteSpaceAround(IReadOnlyList<Node> children, bool[] shown)
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (!shown[i])
                {
                    continue;
                }

                if (!(children[i] is TextNode text))
                {
                    return false;
                }

                foreach (var character in text.Text)
                {
                    if (!char.IsWhiteSpace(character))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
