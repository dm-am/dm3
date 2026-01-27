/**
 * Tiptap BbTab Extension
 *
 * Renders [tab] as an inline tab element (4 non-breaking spaces).
 * Uses data-bb-tag="tab" for lossless round-trip conversion.
 */

import { Node, mergeAttributes } from "@tiptap/core";

export interface BbTabOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    bbTab: {
      /**
       * Insert a tab
       */
      insertTab: () => ReturnType;
    };
  }
}

export const BbTab = Node.create<BbTabOptions>({
  name: "bbTab",

  addOptions() {
    return {
      HTMLAttributes: {},
    };
  },

  group: "inline",

  inline: true,

  atom: true, // Cannot be edited inside

  selectable: true,

  draggable: false,

  addAttributes() {
    return {
      "data-bb-tag": {
        default: "tab",
        parseHTML: () => "tab",
        renderHTML: () => ({ "data-bb-tag": "tab" }),
      },
    };
  },

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
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        class: "bb-tab",
        "data-bb-tag": "tab",
      }),
      "\u00A0\u00A0\u00A0\u00A0", // 4 non-breaking spaces
    ];
  },

  addCommands() {
    return {
      insertTab:
        () =>
        ({ commands }) => {
          return commands.insertContent({
            type: this.name,
          });
        },
    };
  },

  addKeyboardShortcuts() {
    return {
      // Ctrl+Shift+Tab inserts a BBCode tab
      "Mod-Shift-Tab": () => this.editor.commands.insertTab(),
    };
  },
});

export default BbTab;
