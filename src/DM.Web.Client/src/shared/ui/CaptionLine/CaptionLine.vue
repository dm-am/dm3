<script lang="ts">
/**
 * The caption line under a piece of content: who wrote it and when, and what
 * the viewer may do with it, as one run of text with " | " between the parts.
 *
 * Every direct child of the default slot is a part. A child that v-if leaves
 * out is skipped, so a bar never stands at either end of the line and never
 * doubles; a v-for or a <template> is opened, so each of its children is a
 * part of its own. Loose text is a part too, which is rarely what is meant:
 * wrap it in a span together with whatever it belongs to.
 *
 * Every part stands in the line at every width: on a phone the line is the
 * line of the full version, with nothing put away for want of room (owner,
 * 12 October 2026: no "Еще", no "…", no name cut). Where the line has no room
 * it wraps, as it did on a desktop before 5 October.
 *
 * The line breaks between parts and nowhere else, and a bar never opens or
 * closes a line. A part is one box: one that does not fit what is left of a
 * line goes to the next line whole, and only one wider than a whole line wraps,
 * inside itself and no wider than the line. Between two parts stands a gap:
 * the line's own space, the one place the line may break, widened to the width
 * of " | ", with the bar drawn over it by a box taken out of the flow and sized
 * to the gap. A space at the edge of a line is removed together with its word
 * spacing (CSS Text 3, "White Space Processing"), so a gap where the line
 * breaks has no width left, and the bar clipped to it has nothing to show.
 * Where the line runs on, the gap is exactly " | ": the bar stands a space
 * away from either part, just where inline text would put it.
 *
 * A bar that goes with the part before it hangs at the end of the line when
 * the next part wraps; one that goes with the part after it opens the next
 * line. Clipping a bar at the start of a line needs a block around the line,
 * and a line that runs on from a name in the same sentence (a moderator's
 * note) has none. A space disappears at a break in any box.
 *
 * The bar stays text: it is selected and copied with the line, and a copy
 * reads "Автор: X, 09.09.2026 | Удалить" whether or not the line was broken.
 *
 * A part that runs into the next one with a plain space instead of a bar is a
 * <CaptionPart :bar="false">, and one that runs into it with a comma is a
 * <CaptionPart joint=",">: the author and the date, "Автор: X, 05.10.2026",
 * two parts, so a phone can break the line after the comma and keep the date
 * whole. The mark ends the part it follows, so it stays at the end of the
 * line the part ends, and the part takes it along to the next line.
 *
 * A part that is words of the sentence the line runs on from, a title after
 * the dash of a list, is a <CaptionPart flow>: it wraps at its own spaces
 * where the line ends, as text does, instead of going to the next line whole
 * and leaving the dash alone above it.
 *
 * The line owns its size and colour and every part inherits them, which is
 * what draws an action (CaptionAction) and a like (LikeBadge) as ordinary
 * text of the line. A link inside a part keeps its own colour.
 *
 * Written as a render function so the slot's own vnodes become the parts. A
 * template can only draw a vnode it did not create through <component :is>,
 * which builds a new vnode around it, and a shallow mount stubs that one as an
 * anonymous component: every part of the line came out empty.
 */
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
// Normal white-space on the line itself: the spaces between the parts are the
// line's own, and they are its only wrap opportunities. Allowed to be narrower
// than its widest part where a flex row places it: the part then wraps inside
// itself rather than pushing the row past its edge.
.caption-line
  color: $text-muted
  white-space: normal
  min-width: 0

.caption-line--tertiary
  font-size: $tertiary-font-size

.caption-line--secondary
  font-size: $secondary-font-size

// A part, one box: as wide as its text, so it never wraps inside while it
// fits a line and goes to the next line whole when it does not fit what is
// left of this one. Wider than a whole line, it takes the line's width and
// wraps inside, at its spaces and, for a word longer than the line, inside the
// word by the site's rule (Reset.sass, word-wrap on the body). A nowrap run
// stood past the edge there: nowrap turns off every break, that rule's too.
.caption-line__part
  display: inline-block
  max-width: 100%

// Words of the sentence the line runs on from: text of the line, wrapping at
// its spaces where the line ends.
.caption-line__part--flow
  display: inline

// The space between two parts with a bar, widened to " | ": its own advance
// is the space after the bar, and the word spacing adds the space before it
// and the bar itself. 0.505em is the advance of "| " in PT Sans; a thousandth
// more keeps Blink's rounding to 1/64 px from taking a hair off every gap,
// which on a line of five bars shifted the last action by 0.08px.
.caption-line__gap
  position: relative
  word-spacing: 0.506em

// The bar, over the gap and clipped to it: a gap that loses its space at a
// line break loses its width, and the bar goes with it. Out of the flow, so it
// adds neither width nor a wrap opportunity. Its white space is kept, at the
// plain width of a space, so the bar stands one space into the gap, where
// inline text would put it, and is copied as " |". Normal line height is the
// content area of PT Sans, which has no line gap, so the bar stands on the
// baseline of the line.
.caption-line__bar
  position: absolute
  inset: 0
  overflow-x: clip
  white-space: pre
  line-height: normal
  word-spacing: normal

// Firefox sizes an absolute box inside an inline before it removes the space
// at the end of a line, so there the bar keeps the full width of a gap that has
// none and would hang at the end of the line. The gap clips it instead: Firefox
// draws a clip of an inline around what is left of it. Only Firefox, because
// Chromium clips an inline wrongly and loses bars in the middle of a line.
@supports (-moz-appearance: none)
  .caption-line__gap
    clip-path: inset(0)
</style>
