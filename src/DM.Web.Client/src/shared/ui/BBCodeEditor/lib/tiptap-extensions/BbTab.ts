/**
 * Tiptap BbTab Extension
 *
 * Renders [tab] as an inline tab element: three non-breaking spaces, as the
 * server draws it.
 * Uses data-bb-tag="tab" for lossless round-trip conversion.
 */

import { Node, mergeAttributes } from "@tiptap/core";

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    bbTab: {
      insertTab: () => ReturnType;
    };
  }
}

export const BbTab = Node.create({
  name: "bbTab",

  group: "inline",

  inline: true,

  atom: true, // Cannot be edited inside

  parseHTML() {
    return [
      {
        tag: 'span[data-bb-tag="tab"]',
      },
      {
        tag: "span.bb-tab",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "span",
      mergeAttributes({ "data-bb-tag": "tab" }, HTMLAttributes, {
        class: "bb-tab",
      }),
      "\u00A0\u00A0\u00A0", // 3 non-breaking spaces, as on the server
    ];
  },

  addCommands() {
    return {
      insertTab:
        () =>
        ({ commands }) =>
          commands.insertContent({ type: this.name }),
    };
  },

  addKeyboardShortcuts() {
    return {
      // Ctrl+Shift+Tab inserts a BBCode tab
      "Mod-Shift-Tab": () => this.editor.commands.insertTab(),
    };
  },
});
