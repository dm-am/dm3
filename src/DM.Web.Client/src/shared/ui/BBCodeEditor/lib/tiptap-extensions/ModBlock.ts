/**
 * Tiptap ModBlock Extension
 *
 * Renders [mod]content[/mod] as a moderator-styled block with green border.
 * Uses data-bb-tag="mod" for lossless round-trip conversion.
 */

import { Node, mergeAttributes } from "@tiptap/core";

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    modBlock: {
      toggleModBlock: () => ReturnType;
    };
  }
}

export const ModBlock = Node.create({
  name: "modBlock",

  group: "block",

  content: "block+",

  defining: true,

  parseHTML() {
    return [
      {
        tag: 'div[data-bb-tag="mod"]',
      },
      {
        tag: "div.bb-mod",
      },
      {
        tag: "div.mod-block",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "div",
      mergeAttributes({ "data-bb-tag": "mod" }, HTMLAttributes, {
        class: "bb-mod",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      toggleModBlock:
        () =>
        ({ commands }) =>
          commands.toggleWrap(this.name),
    };
  },
});
