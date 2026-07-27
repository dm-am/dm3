/**
 * Tiptap ModBlock Extension
 *
 * Renders [mod]content[/mod] as a moderator-styled block with green border.
 * Uses data-bb-tag="mod" for lossless round-trip conversion.
 */

import { Node, mergeAttributes } from "@tiptap/core";

export interface ModBlockOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    modBlock: {
      /**
       * Set a mod block
       */
      setModBlock: () => ReturnType;
      /**
       * Toggle a mod block
       */
      toggleModBlock: () => ReturnType;
      /**
       * Unset a mod block
       */
      unsetModBlock: () => ReturnType;
    };
  }
}

export const ModBlock = Node.create<ModBlockOptions>({
  name: "modBlock",

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
        default: "mod",
        parseHTML: () => "mod",
        renderHTML: () => ({ "data-bb-tag": "mod" }),
      },
    };
  },

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
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        class: "bb-mod",
        "data-bb-tag": "mod",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      setModBlock:
        () =>
        ({ commands }) => {
          return commands.wrapIn(this.name);
        },
      toggleModBlock:
        () =>
        ({ commands }) => {
          return commands.toggleWrap(this.name);
        },
      unsetModBlock:
        () =>
        ({ commands }) => {
          return commands.lift(this.name);
        },
    };
  },
});

export default ModBlock;
