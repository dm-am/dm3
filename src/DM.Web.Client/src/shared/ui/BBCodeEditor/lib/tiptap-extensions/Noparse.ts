/**
 * Tiptap Noparse Extension
 *
 * Renders [noparse]content[/noparse] as plain text without BBCode processing.
 * Uses data-bb-tag="noparse" for lossless round-trip conversion.
 */

import { Mark, mergeAttributes } from "@tiptap/core";

export interface NoparseOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    noparse: {
      /**
       * Set noparse mark
       */
      setNoparse: () => ReturnType;
      /**
       * Toggle noparse mark
       */
      toggleNoparse: () => ReturnType;
      /**
       * Unset noparse mark
       */
      unsetNoparse: () => ReturnType;
    };
  }
}

export const Noparse = Mark.create<NoparseOptions>({
  name: "noparse",

  addOptions() {
    return {
      HTMLAttributes: {},
    };
  },

  // The innermost mark: what it holds is text, so every other mark on its
  // text is written around it (see the order of the marks in
  // editorExtensions.ts). No mark is excluded from its text for that reason.
  priority: 50,

  addAttributes() {
    return {
      "data-bb-tag": {
        default: "noparse",
        parseHTML: () => "noparse",
        renderHTML: () => ({ "data-bb-tag": "noparse" }),
      },
    };
  },

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
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        class: "bb-noparse",
        "data-bb-tag": "noparse",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      setNoparse:
        () =>
        ({ commands }) => {
          return commands.setMark(this.name);
        },
      toggleNoparse:
        () =>
        ({ commands }) => {
          return commands.toggleMark(this.name);
        },
      unsetNoparse:
        () =>
        ({ commands }) => {
          return commands.unsetMark(this.name);
        },
    };
  },
});

export default Noparse;
