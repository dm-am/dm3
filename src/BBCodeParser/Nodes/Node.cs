// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System;
using System.Collections.Generic;
using System.Text;

namespace BBCodeParser.Nodes
{
    public abstract class Node
    {
        protected List<Node> ChildNodes { get; set; }
        internal Node ParentNode { get; set; }

        // DM3 addition against the original: the children in document order, for
        // a reader of the tree that is not its renderer.
        public IReadOnlyList<Node> Children => (IReadOnlyList<Node>)ChildNodes ?? Array.Empty<Node>();

        // DM3 addition against the original: the node this one stands in, null for
        // the root, for a tag whose markup depends on where it stands.
        public Node Parent => ParentNode;

        // DM3 change against the original: the recursive renderers are replaced by
        // Emit and the iterative walk in Render, because a StackOverflowException
        // on deep nesting cannot be caught and kills the process.

        /// <summary>
        /// Write this node's own output and queue whatever has to follow it:
        /// its children, and for a tag its closing markup.
        /// </summary>
        internal abstract void Emit(
            StringBuilder output,
            Stack<EmitStep> pending,
            EmitStep step,
            Dictionary<string, string> securitySubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue);

        /// <summary>
        /// Walk the tree rooted at <paramref name="root"/> without recursion
        /// and return the requested output.
        /// </summary>
        internal static string Render(
            Node root,
            EmitMode mode,
            Dictionary<string, string> securitySubstitutions,
            Dictionary<string, string> aliasSubstitutions,
            Func<Node, bool> filter,
            Func<Node, string, string> filterAttributeValue)
        {
            var output = new StringBuilder();
            var pending = new Stack<EmitStep>();
            pending.Push(new EmitStep(root, mode, aliasSubstitutions));

            // DM3 addition against the original: a tag the filter leaves with
            // nothing to hold is left out with what the filter erased (see
            // Visibility).
            filter = Visibility.Of(filter);

            // DM3 addition against the original: set when the last thing written
            // closed a block that ends its line (Tag.DropsLineBreakAfter), and
            // spent on whatever is written next. Only text that follows the
            // closing tag directly loses its first line break: a tag between the
            // two, even an empty one, keeps it.
            var lineEnded = false;

            while (pending.Count > 0)
            {
                var step = pending.Pop();
                if (step.Literal != null)
                {
                    output.Append(step.Literal);
                    lineEnded = step.EndsLine;
                    continue;
                }

                if (lineEnded && step.Mode == EmitMode.Html && step.Node is TextNode text)
                {
                    step = new EmitStep(text.WithoutLeadingLineBreak(), step.Mode, step.AliasSubstitutions);
                }

                lineEnded = false;
                step.Node.Emit(output, pending, step, securitySubstitutions, filter, filterAttributeValue);
            }

            return output.ToString();
        }

        /// <summary>
        /// Queue this node's children so that the first child is emitted first.
        /// </summary>
        /// <remarks>
        /// Pushed back to front, so a closing tag the caller pushed first comes off
        /// after all of them.
        /// </remarks>
        internal void PushChildren(
            Stack<EmitStep> pending,
            EmitMode childMode,
            Dictionary<string, string> childAliasSubstitutions,
            Func<Node, bool> filter)
        {
            // DM3 addition against the original: a line left with nothing but what
            // the filter erases goes with one of its line breaks (see ErasedLines).
            bool[] shown = null;
            Dictionary<int, TextNode> trimmed = null;
            if (filter != null)
            {
                shown = new bool[ChildNodes.Count];
                for (var i = 0; i < ChildNodes.Count; i++)
                {
                    shown[i] = filter(ChildNodes[i]);
                }

                trimmed = ErasedLines.Trim(this, shown);
            }

            for (var i = ChildNodes.Count - 1; i >= 0; i--)
            {
                var childNode = ChildNodes[i];
                if (shown != null && !shown[i])
                {
                    continue;
                }

                if (trimmed != null && trimmed.TryGetValue(i, out var text))
                {
                    if (text == null)
                    {
                        continue;
                    }

                    childNode = text;
                }

                pending.Push(new EmitStep(childNode, childMode, childAliasSubstitutions));
            }
        }

        public virtual void AddChild(Node node)
        {
            ChildNodes.Add(node);
        }

        // DM3 addition against the original: whether what this node holds runs to
        // the end of the text, for ErasedLines. Remembered per node, so a deep tag
        // soup does not cost the square of its depth.
        internal bool ReachesTextEnd()
        {
            var path = new List<Node>();
            var node = this;
            bool reaches;
            while (true)
            {
                if (node.reachesTextEnd.HasValue)
                {
                    reaches = node.reachesTextEnd.Value;
                    break;
                }

                if (node.ParentNode == null)
                {
                    reaches = node is NodeTree;
                    node.reachesTextEnd = reaches;
                    break;
                }

                var siblings = node.ParentNode.ChildNodes;
                if ((node is TagNode tag && tag.IsClosed) || !ReferenceEquals(siblings[siblings.Count - 1], node))
                {
                    reaches = false;
                    node.reachesTextEnd = false;
                    break;
                }

                path.Add(node);
                node = node.ParentNode;
            }

            // Every node on the way up stood last and open, so each answers as its parent.
            foreach (var step in path)
            {
                step.reachesTextEnd = reaches;
            }

            return reaches;
        }

        private bool? reachesTextEnd;
    }
}
