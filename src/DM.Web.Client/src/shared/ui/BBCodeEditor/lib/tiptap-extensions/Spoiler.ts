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
 * Ctrl+Shift+S is the editor's own (BBCodeEditor.vue), not this extension's: a
 * keymap binding matches the letter the layout types, so on a Russian layout
 * the key fell through to the strike of StarterKit, bound to the same key.
 */

import { Node, mergeAttributes } from "@tiptap/core";
import { VueNodeViewRenderer } from "@tiptap/vue-3";
import SpoilerView from "./SpoilerView.vue";

export interface SpoilerOptions {
  HTMLAttributes: Record<string, unknown>;
}

export interface SpoilerAttributes {
  /** The caption of the button; null for the default one. */
  text?: string | null;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    spoiler: {
      /**
       * Set a spoiler block
       */
      setSpoiler: (attributes?: SpoilerAttributes) => ReturnType;
      /**
       * Toggle a spoiler block
       */
      toggleSpoiler: (attributes?: SpoilerAttributes) => ReturnType;
      /**
       * Unset a spoiler block
       */
      unsetSpoiler: () => ReturnType;
    };
  }
}

export const Spoiler = Node.create<SpoilerOptions>({
  name: "spoiler",

  addOptions() {
    return {
      HTMLAttributes: {},
    };
  },

  group: "block",

  content: "block+",

  defining: true,

  addAttributes() {
    return {
      "data-bb-tag": {
        default: "spoiler",
        parseHTML: () => "spoiler",
        renderHTML: () => ({ "data-bb-tag": "spoiler" }),
      },
      text: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-text"),
        renderHTML: (attributes) =>
          attributes.text === null ? {} : { "data-bb-text": attributes.text },
      },
      open: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-open"),
        renderHTML: (attributes) =>
          attributes.open === null ? {} : { "data-bb-open": attributes.open },
      },
      close: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-close"),
        renderHTML: (attributes) =>
          attributes.close === null
            ? {}
            : { "data-bb-close": attributes.close },
      },
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
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        class: "bb-spoiler",
        "data-bb-tag": "spoiler",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      setSpoiler:
        (attributes) =>
        ({ commands }) => {
          return commands.wrapIn(this.name, attributes);
        },
      toggleSpoiler:
        (attributes) =>
        ({ commands }) => {
          return commands.toggleWrap(this.name, attributes);
        },
      unsetSpoiler:
        () =>
        ({ commands }) => {
          return commands.lift(this.name);
        },
    };
  },

  addNodeView() {
    return VueNodeViewRenderer(SpoilerView);
  },
});

export default Spoiler;
