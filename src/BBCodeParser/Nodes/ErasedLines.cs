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
    /// An erased block left on a line of its own would leave an empty line that
    /// says something stood there. The siblings are read as lines between CR LF or
    /// LF breaks, and a line of nothing but erased nodes goes with the break before
    /// it, else the one after. The whole text starts and ends lines, a tag does not,
    /// except the last line of a tag nobody closed. A text left with nothing but
    /// white space is drawn as nothing, as the parser reads white space alone.
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

            if (atTextStart && atTextEnd && IsOnlyWhiteSpaceAround(children, shown))
            {
                return EveryTextLeftWithNothing(children);
            }

            var lines = ScanLines(children, shown);
            var dropped = DroppedBreaks(lines, atTextStart, atTextEnd);
            return dropped == null ? null : WithoutDroppedBreaks(children, lines.Breaks, dropped);
        }

        /// <summary>
        /// The line breaks among the children, and for each line between them
        /// whether it holds anything drawn and anything erased.
        /// </summary>
        private sealed class Lines
        {
            public List<(int Child, int At, int Length)> Breaks { get; } = new List<(int, int, int)>();
            public List<bool> Content { get; } = new List<bool> { false };
            public List<bool> Erased { get; } = new List<bool> { false };
        }

        private static Dictionary<int, TextNode> EveryTextLeftWithNothing(IReadOnlyList<Node> children)
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

        private static Lines ScanLines(IReadOnlyList<Node> children, bool[] shown)
        {
            var lines = new Lines();
            var content = lines.Content;
            var erased = lines.Erased;

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not TextNode text)
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

                ScanText(i, text.Text, lines);
            }

            return lines;
        }

        private static void ScanText(int child, string value, Lines lines)
        {
            var content = lines.Content;
            for (var at = 0; at < value.Length; at++)
            {
                var length = value[at] switch
                {
                    '\n' => 1,
                    '\r' when at + 1 < value.Length && value[at + 1] == '\n' => 2,
                    _ => 0
                };
                if (length == 0)
                {
                    content[content.Count - 1] = true;
                    continue;
                }

                lines.Breaks.Add((child, at, length));
                content.Add(false);
                lines.Erased.Add(false);
                at += length - 1;
            }
        }

        /// <summary>
        /// By break, whether it goes with an erased line. Null when none goes.
        /// </summary>
        private static bool[] DroppedBreaks(Lines lines, bool atTextStart, bool atTextEnd)
        {
            var content = lines.Content;
            var erased = lines.Erased;
            var count = content.Count;
            var dropped = new bool[lines.Breaks.Count];
            var any = false;
            for (var line = 0; line < count; line++)
            {
                if (content[line] || !erased[line] ||
                    (line == 0 && !atTextStart) || (line == count - 1 && !atTextEnd))
                {
                    continue;
                }

                var before = line - 1;
                var after = line;
                if (before >= 0 && !dropped[before])
                {
                    dropped[before] = true;
                    any = true;
                }
                else if (after < lines.Breaks.Count && !dropped[after])
                {
                    dropped[after] = true;
                    any = true;
                }
            }

            return any ? dropped : null;
        }

        private static Dictionary<int, TextNode> WithoutDroppedBreaks(
            IReadOnlyList<Node> children,
            List<(int Child, int At, int Length)> breaks,
            bool[] dropped)
        {
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
                // Not emitted, so that it cannot stand in for the text DropsLineBreakAfter strips.
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

                if (children[i] is not TextNode text || !string.IsNullOrWhiteSpace(text.Text))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
