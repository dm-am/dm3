/**
 * Tiptap BbLooseItem Extension
 *
 * A list item with no list around it: [li] written outside [ul] and [ol], and
 * the items of a list whose [ul] or [ol] was never closed, which prints as text
 * while its items stay items. The converter draws such an item as a div with
 * data-bb-tag="li", since a li outside a list is no HTML. Without a node of its
 * own the visual mode read that div as bare paragraphs, and the [li] tags were
 * gone at the next save.
 *
 * The node draws nothing of its own, so the visual mode looks the same as
 * before, and keeps a tag typed in another case, [LI] or [/Li], in
 * data-bb-open and data-bb-close for the way back.
 */

import { Node, mergeAttributes } from "@tiptap/core";
import { kept } from "./kept";

export const BbLooseItem = Node.create({
  name: "bbLooseItem",

  group: "block",

  content: "block+",

  defining: true,

  addAttributes() {
    return {
      open: kept("data-bb-open"),
      close: kept("data-bb-close"),
    };
  },

  parseHTML() {
    return [{ tag: 'div[data-bb-tag="li"]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ["div", mergeAttributes(HTMLAttributes, { "data-bb-tag": "li" }), 0];
  },
});
