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

  // Noparse should be the outermost mark
  priority: 1000,

  // Exclude other marks from being applied inside noparse
  excludes: "_",

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
