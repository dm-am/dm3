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

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    nsfw: {
      toggleNsfw: () => ReturnType;
    };
  }
}

export const Nsfw = Node.create({
  name: "nsfw",

  group: "block",

  content: "block+",

  defining: true,

  addAttributes() {
    return {
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
      mergeAttributes({ "data-bb-tag": "nsfw" }, HTMLAttributes, {
        class: "bb-nsfw",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      toggleNsfw:
        () =>
        ({ commands }) =>
          commands.toggleWrap(this.name),
    };
  },

  addNodeView() {
    return VueNodeViewRenderer(NsfwView);
  },
});
