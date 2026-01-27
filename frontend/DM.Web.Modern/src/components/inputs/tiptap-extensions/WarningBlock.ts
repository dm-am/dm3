/**
 * Tiptap WarningBlock Extension
 *
 * Renders [warning]content[/warning] as a warning-styled block with red border.
 * Similar to [mod] but with red styling for warnings/errors.
 * Uses data-bb-tag="warning" for lossless round-trip conversion.
 */

import { Node, mergeAttributes } from "@tiptap/core";

export interface WarningBlockOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    warningBlock: {
      /**
       * Set a warning block
       */
      setWarningBlock: () => ReturnType;
      /**
       * Toggle a warning block
       */
      toggleWarningBlock: () => ReturnType;
      /**
       * Unset a warning block
       */
      unsetWarningBlock: () => ReturnType;
    };
  }
}

export const WarningBlock = Node.create<WarningBlockOptions>({
  name: "warningBlock",

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
        default: "warning",
        parseHTML: () => "warning",
        renderHTML: () => ({ "data-bb-tag": "warning" }),
      },
    };
  },

  parseHTML() {
    return [
      {
        tag: 'div[data-bb-tag="warning"]',
      },
      {
        tag: "div.bb-warning",
      },
      {
        tag: "div.warning-block",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "div",
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        class: "bb-warning",
        "data-bb-tag": "warning",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      setWarningBlock:
        () =>
        ({ commands }) => {
          return commands.wrapIn(this.name);
        },
      toggleWarningBlock:
        () =>
        ({ commands }) => {
          return commands.toggleWrap(this.name);
        },
      unsetWarningBlock:
        () =>
        ({ commands }) => {
          return commands.lift(this.name);
        },
    };
  },
});

export default WarningBlock;
