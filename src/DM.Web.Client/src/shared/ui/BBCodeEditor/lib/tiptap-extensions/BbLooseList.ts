/**
 * Tiptap BbLooseList Extension
 *
 * A [ul] or [ol] that holds something besides its items: words, a spoiler, a
 * quote or a hidden block written between the items. A list of the visual
 * mode holds items and nothing else, and it put the rest into items of its own
 * or moved it out of the list, and out of a hidden block around the list with
 * it: a spoiler written in a list in a hidden block came back as a spoiler
 * every reader saw. The converter draws such a list as a div with
 * data-bb-tag="ul" or "ol" and data-bb-loose, holding its items as loose items
 * (BbLooseItem) and everything else as it stands, so the visual mode keeps it
 * in the order it was written and the way back gives it as typed.
 *
 * The node draws nothing of its own. A tag typed in another case rides in
 * data-bb-open and data-bb-close (BbSpelling).
 */

import { Node, mergeAttributes } from "@tiptap/core";

export const BbLooseList = Node.create({
  name: "bbLooseList",

  group: "block",

  content: "block+",

  defining: true,

  addAttributes() {
    return {
      tag: {
        default: "ul",
        parseHTML: (element) =>
          element.getAttribute("data-bb-tag") === "ol" ? "ol" : "ul",
        renderHTML: (attributes) => ({ "data-bb-tag": attributes.tag }),
      },
    };
  },

  parseHTML() {
    return [
      { tag: 'div[data-bb-tag="ul"][data-bb-loose]' },
      { tag: 'div[data-bb-tag="ol"][data-bb-loose]' },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "div",
      mergeAttributes(HTMLAttributes, { "data-bb-loose": "true" }),
      0,
    ];
  },
});

export default BbLooseList;
