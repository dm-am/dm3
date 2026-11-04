<script lang="ts">
// Parts joined by " | ", breaking between parts alone: the bar is clipped to the breakable space under it,
// so it vanishes where the line breaks and stays text when copied. <CaptionPart :bar="false"> joins with a
// space, joint="," with a comma, flow wraps as words of the sentence. A render function, so the slot's own
// vnodes become the parts: <component :is> would wrap them, and a shallow mount stubbed those empty.
import {
  Comment,
  Fragment,
  Text,
  defineComponent,
  h,
  type PropType,
  type VNode,
  type VNodeArrayChildren,
} from "vue";

/** The parts of a slot: fragments opened, comments and blank text dropped. */
function flatten(nodes: VNode[], out: VNode[] = []): VNode[] {
  for (const node of nodes) {
    if (node.type === Comment) continue;
    if (node.type === Fragment) {
      if (Array.isArray(node.children)) flatten(node.children as VNode[], out);
      continue;
    }
    if (node.type === Text && !String(node.children ?? "").trim()) continue;
    out.push(node);
  }
  return out;
}

/**
 * What a CaptionPart says about the way it joins the next part: its mark, or
 * true for a plain space (:bar="false"), or nothing for the bar. Read off the
 * props of any component rather than matched by type, so a CaptionPart that a
 * shallow mount has replaced with a stub still joins the way it says.
 */
function joinOf(node: VNode): string | true | null {
  if (typeof node.type !== "object" || !node.props) return null;
  const joint = node.props.joint;
  if (typeof joint === "string" && joint) return joint;
  return node.props.bar === false ? true : null;
}

/** A <CaptionPart flow>, stubbed or not: "flow" written bare is "" on the vnode. */
function flows(node: VNode): boolean {
  if (typeof node.type !== "object" || !node.props) return false;
  const flow = node.props.flow;
  return flow === "" || flow === true;
}

/** The gap between two parts: the breakable space, and the bar drawn on it. */
function gap(): VNode {
  return h("span", { class: "caption-line__gap" }, [
    h("span", { class: "caption-line__bar", "aria-hidden": "true" }, " |"),
    " ",
  ]);
}

export default defineComponent({
  name: "CaptionLine",
  props: {
    /** The caption size, or the secondary one for a line that stands among
     * secondary text (the header of a moderator's note). */
    size: {
      type: String as PropType<"tertiary" | "secondary">,
      default: "tertiary",
    },
  },
  setup(props, { slots }) {
    // The slot is called inside this render, so a part that appears or goes
    // away re-renders the line.
    return () => {
      const parts = flatten(slots.default?.() ?? []);
      const children: VNodeArrayChildren = [];
      parts.forEach((part, index) => {
        const last = index === parts.length - 1;
        const join = last ? null : joinOf(part);
        // The mark is the line's, as the bar is: copied, and heard as the
        // space after it.
        const mark =
          typeof join === "string"
            ? [
                h(
                  "span",
                  { class: "caption-line__mark", "aria-hidden": "true" },
                  join,
                ),
              ]
            : [];
        children.push(
          h(
            "span",
            {
              class: [
                "caption-line__part",
                flows(part) && "caption-line__part--flow",
              ],
            },
            [part, ...mark],
          ),
        );
        if (!last) children.push(join === null ? gap() : " ");
      });
      return h(
        "span",
        { class: ["caption-line", `caption-line--${props.size}`] },
        children,
      );
    };
  },
});
</script>

<style scoped lang="sass">
// Allowed narrower than its widest part: the part then wraps inside itself.
.caption-line
  color: $text-muted
  white-space: normal
  min-width: 0

.caption-line--tertiary
  font-size: $tertiary-font-size

.caption-line--secondary
  font-size: $secondary-font-size

// One box per part, sent to the next line whole; nowrap would also turn off the body's overflow-wrap.
.caption-line__part
  display: inline-block
  max-width: 100%

.caption-line__part--flow
  display: inline

// 0.505em is the advance of "| " in PT Sans; a thousandth more keeps Blink's 1/64px rounding off every gap.
.caption-line__gap
  position: relative
  word-spacing: 0.506em

// Clipped to the gap: a gap that loses its space at a line break loses its width, and the bar with it.
.caption-line__bar
  position: absolute
  inset: 0
  overflow-x: clip
  white-space: pre
  line-height: normal
  word-spacing: normal

// Firefox sizes the bar before removing the line-end space; Chromium clips inlines wrongly, so Firefox only.
@supports (-moz-appearance: none)
  .caption-line__gap
    clip-path: inset(0)
</style>
