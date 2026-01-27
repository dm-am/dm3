/**
 * Tiptap Spoiler Extension
 *
 * Renders [spoiler]content[/spoiler] as an expandable block.
 * Uses data-bb-tag="spoiler" for lossless round-trip conversion.
 * Features interactive NodeView with click-to-expand.
 */

import { Node, mergeAttributes } from "@tiptap/core";
import { VueNodeViewRenderer } from "@tiptap/vue-3";
import SpoilerView from "./SpoilerView.vue";

export interface SpoilerOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    spoiler: {
      /**
       * Set a spoiler block
       */
      setSpoiler: () => ReturnType;
      /**
       * Toggle a spoiler block
       */
      toggleSpoiler: () => ReturnType;
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
        () =>
        ({ commands }) => {
          return commands.wrapIn(this.name);
        },
      toggleSpoiler:
        () =>
        ({ commands }) => {
          return commands.toggleWrap(this.name);
        },
      unsetSpoiler:
        () =>
        ({ commands }) => {
          return commands.lift(this.name);
        },
    };
  },

  addKeyboardShortcuts() {
    return {
      "Mod-Shift-S": () => this.editor.commands.toggleSpoiler(),
    };
  },

  addNodeView() {
    return VueNodeViewRenderer(SpoilerView);
  },
});

export default Spoiler;
