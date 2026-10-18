/**
 * Tiptap BbQuote Extension
 *
 * Renders [quote] as styled blockquotes.
 * Uses data-bb-tag="quote" for lossless round-trip conversion.
 *
 * The tag has no attribute, as in DM2: a quote is a way to set text apart, and
 * the block carries nothing but the text inside it.
 */

import { Node, mergeAttributes } from "@tiptap/core";

export interface BbQuoteOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    bbQuote: {
      /**
       * Set a quote block
       */
      setQuote: () => ReturnType;
      /**
       * Toggle a quote block
       */
      toggleQuote: () => ReturnType;
      /**
       * Unset a quote block
       */
      unsetQuote: () => ReturnType;
    };
  }
}

export const BbQuote = Node.create<BbQuoteOptions>({
  name: "bbQuote",

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
        default: "quote",
        parseHTML: () => "quote",
        renderHTML: () => ({ "data-bb-tag": "quote" }),
      },
    };
  },

  parseHTML() {
    return [
      {
        tag: 'blockquote[data-bb-tag="quote"]',
      },
      {
        tag: "blockquote.bb-quote",
      },
      // Fallback - parse any blockquote
      {
        tag: "blockquote",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    const attrs = mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
      class: "bb-quote",
      "data-bb-tag": "quote",
    });

    return ["blockquote", attrs, 0];
  },

  addCommands() {
    return {
      setQuote:
        () =>
        ({ commands }) => {
          return commands.wrapIn(this.name);
        },
      toggleQuote:
        () =>
        ({ commands }) => {
          return commands.toggleWrap(this.name);
        },
      unsetQuote:
        () =>
        ({ commands }) => {
          return commands.lift(this.name);
        },
    };
  },

  addKeyboardShortcuts() {
    return {
      "Mod-Shift-Q": () => this.editor.commands.toggleQuote(),
    };
  },
});

export default BbQuote;
