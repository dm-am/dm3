/**
 * Tiptap NSFW Extension
 *
 * Renders [nsfw]content[/nsfw] as a blurred/hidden block.
 * Uses data-bb-tag="nsfw" for lossless round-trip conversion.
 * Features interactive NodeView with click-to-reveal.
 */

import { Node, mergeAttributes } from "@tiptap/core";
import { VueNodeViewRenderer } from "@tiptap/vue-3";
import NsfwView from "./NsfwView.vue";

export interface NsfwOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    nsfw: {
      /**
       * Set a NSFW block
       */
      setNsfw: () => ReturnType;
      /**
       * Toggle a NSFW block
       */
      toggleNsfw: () => ReturnType;
      /**
       * Unset a NSFW block
       */
      unsetNsfw: () => ReturnType;
    };
  }
}

export const Nsfw = Node.create<NsfwOptions>({
  name: "nsfw",

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
        default: "nsfw",
        parseHTML: () => "nsfw",
        renderHTML: () => ({ "data-bb-tag": "nsfw" }),
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
        tag: 'div[data-bb-tag="nsfw"]',
      },
      {
        tag: "div.bb-nsfw",
      },
      {
        tag: "div.nsfw-spoiler",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "div",
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        class: "bb-nsfw",
        "data-bb-tag": "nsfw",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      setNsfw:
        () =>
        ({ commands }) => {
          return commands.wrapIn(this.name);
        },
      toggleNsfw:
        () =>
        ({ commands }) => {
          return commands.toggleWrap(this.name);
        },
      unsetNsfw:
        () =>
        ({ commands }) => {
          return commands.lift(this.name);
        },
    };
  },

  addNodeView() {
    return VueNodeViewRenderer(NsfwView);
  },
});

export default Nsfw;
