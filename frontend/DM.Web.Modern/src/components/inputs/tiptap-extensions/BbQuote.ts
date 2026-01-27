/**
 * Tiptap BbQuote Extension
 *
 * Renders [quote] as styled blockquotes.
 * Uses data-bb-tag="quote" for lossless round-trip conversion.
 *
 * Note: [quote=author] is NOT supported. Author parameter is intentionally
 * not implemented - quotes are anonymous blocks only.
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
      // Legacy fallback - parse any blockquote (ignore author if present)
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
