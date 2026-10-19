/**
 * Tiptap Spoiler Extension
 *
 * Renders [spoiler]content[/spoiler] and [spoiler text="caption"]content[/spoiler]
 * as an expandable block. Uses data-bb-tag="spoiler" for lossless round-trip
 * conversion; the caption rides in data-bb-text, an opening spelled other than
 * the canonical way in data-bb-open, and a closing tag typed in another case,
 * [/SPOILER], in data-bb-close. Features interactive NodeView with
 * click-to-expand.
 *
 * Ctrl+Shift+S is the editor's own (visualEditorProps in editorExtensions.ts),
 * not this extension's: a keymap binding matches the letter the layout types,
 * so on a Russian layout the key fell through to the strike of StarterKit,
 * bound to the same key.
 */

import { Node, mergeAttributes } from "@tiptap/core";
import { VueNodeViewRenderer } from "@tiptap/vue-3";
import SpoilerView from "./SpoilerView.vue";
import { kept } from "./kept";

interface SpoilerAttributes {
  /** The caption of the button; null for the default one. */
  text?: string | null;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    spoiler: {
      setSpoiler: (attributes?: SpoilerAttributes) => ReturnType;
      toggleSpoiler: (attributes?: SpoilerAttributes) => ReturnType;
    };
  }
}

export const Spoiler = Node.create({
  name: "spoiler",

  group: "block",

  content: "block+",

  defining: true,

  addAttributes() {
    return {
      text: kept("data-bb-text"),
      open: kept("data-bb-open"),
      close: kept("data-bb-close"),
      collapsed: {
        default: true,
        parseHTML: (element) =>
          element.getAttribute("data-collapsed") !== "false",
        renderHTML: (attributes) => ({
          "data-collapsed": attributes.collapsed ? "true" : "false",
        }),
      },
    };
  },

  parseHTML() {
    return [
      {
        tag: 'div[data-bb-tag="spoiler"]',
      },
      {
        tag: "div.bb-spoiler",
      },
      {
        tag: "div.spoiler",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "div",
      mergeAttributes({ "data-bb-tag": "spoiler" }, HTMLAttributes, {
        class: "bb-spoiler",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      setSpoiler:
        (attributes) =>
        ({ commands }) =>
          commands.wrapIn(this.name, attributes),
      toggleSpoiler:
        (attributes) =>
        ({ commands }) =>
          commands.toggleWrap(this.name, attributes),
    };
  },

  addNodeView() {
    return VueNodeViewRenderer(SpoilerView);
  },
});
