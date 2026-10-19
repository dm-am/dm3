/**
 * Tiptap Noparse Extension
 *
 * Renders [noparse]content[/noparse] as plain text without BBCode processing.
 * Uses data-bb-tag="noparse" for lossless round-trip conversion.
 */

import { Mark, mergeAttributes } from "@tiptap/core";

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    noparse: {
      toggleNoparse: () => ReturnType;
    };
  }
}

export const Noparse = Mark.create({
  name: "noparse",

  // The innermost mark: what it holds is text, so every other mark on its
  // text is written around it (see the order of the marks in
  // editorExtensions.ts). No mark is excluded from its text for that reason.
  priority: 50,

  parseHTML() {
    return [
      {
        tag: 'span[data-bb-tag="noparse"]',
      },
      {
        tag: "span.bb-noparse",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "span",
      mergeAttributes({ "data-bb-tag": "noparse" }, HTMLAttributes, {
        class: "bb-noparse",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      toggleNoparse:
        () =>
        ({ commands }) =>
          commands.toggleMark(this.name),
    };
  },
});
