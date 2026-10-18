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

        // DM3 addition against the original: the children of the node, in
        // document order, for a reader of the tree that is not its renderer.
        public IReadOnlyList<Node> Children => (IReadOnlyList<Node>)ChildNodes ?? Array.Empty<Node>();

        // DM3 addition against the original: the node this one stands in, null
        // for the root, for a tag whose markup depends on where it stands.
        public Node Parent => ParentNode;

        // DM3 change against the original: the three internal abstract
        // renderers - ToHtml, ToText and ToBb, each overridden once per node
        // type and each calling itself on every child - are replaced by one
        // abstract Emit plus the iterative walk in Render below.
        //
        // Why: the recursion was one stack frame per level of nesting, and a
        // StackOverflowException is not catchable in .NET - the process is
        // killed on the spot. Twenty kilobytes of text ([b] repeated six
        // thousand times, no closing tags needed) reached the limit of the
        // default 1 MB stack and took the whole API process down, and took it
        // down again on every later attempt to render the page that post was
        // on. The parser's own depth guard stood at 6000 and so never fired
        // before the crash. Depth now costs heap, which is bounded by the
        // guard in BbParser and by the length of the input, and no input
        // reaches the runtime's stack limit any more.
        //
        // The same rewrite removes the quadratic string building the original
        // had: every level used to allocate its own StringBuilder and hand its
        // ToString() to the parent, which copied it into its own buffer, so
        // text near the leaves was copied once per level above it. One shared
        // buffer for the whole walk copies it once.
        //
        // And it closes a third defect for free: TagNode.ToHtml passed only
        // filter down to its children and dropped filterAttributeValue, so the
        // attribute transform ran on the children of the root and on nobody
        // deeper. One walk carries both to every node.

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
        /// Pushed back to front, because a stack hands them back in reverse.
        /// Anything the caller has already pushed - a closing tag - therefore
        /// comes off after all of them, which is what the recursive version got
        /// from returning to its caller.
        /// </remarks>
        internal void PushChildren(
            Stack<EmitStep> pending,
            EmitMode childMode,
            Dictionary<string, string> childAliasSubstitutions,
            Func<Node, bool> filter)
        {
            // DM3 addition against the original: a line left with nothing but
            // what the filter erases goes with one of its line breaks (see
            // ErasedLines). The filter is asked once per child, and the answer
            // serves both the lines and the walk.
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

        // DM3 addition against the original: whether what this node holds runs
        // to the very end of the text - the root does, and so does a tag nobody
        // closed that stands last in a node that does. Read by ErasedLines, for
        // which the end of the text ends a line.
        //
        // Remembered per node, and asked without recursion: a tag soup nests
        // thousands of levels deep, and a walk to the root from every level of
        // it would cost the square of the depth.
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
                if ((node is TagNode tag && tag.IsClosed) ||
                    siblings.Count == 0 || !ReferenceEquals(siblings[siblings.Count - 1], node))
                {
                    reaches = false;
                    node.reachesTextEnd = false;
                    break;
                }

                path.Add(node);
                node = node.ParentNode;
            }

            // Every node on the way up stood last and open, so each reaches the
            // end exactly when the one it stands in does.
            foreach (var step in path)
            {
                step.reachesTextEnd = reaches;
            }

            return reaches;
        }

        private bool? reachesTextEnd;
    }
}
