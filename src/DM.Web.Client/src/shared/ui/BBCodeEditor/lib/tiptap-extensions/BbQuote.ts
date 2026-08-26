/**
 * Tiptap BbQuote Extension
 *
 * Renders [quote] as styled blockquotes.
 * Uses data-bb-tag="quote" for lossless round-trip conversion.
 *
 * The author travels in data-bb-author, and has to: the Quote action fills the
 * composer with somebody else's line attributed to them, and a node that does
 * not hold the attribute drops the attribution the moment the visual mode
 * parses the HTML — silently, and only in one of the two modes, so the same
 * quotation saved from the source mode kept its author and saved from the
 * visual one lost it. A quotation without an author stays a legal form: the DM2
 * tag has no attribute at all, so the whole imported archive is that shape.
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
      "data-bb-author": {
        default: null,
        parseHTML: (element: HTMLElement) =>
          element.getAttribute("data-bb-author"),
        renderHTML: (attributes: Record<string, unknown>) => {
          const author = attributes["data-bb-author"];
          // No attribute at all rather than an empty one: bbcode.ts reads the
          // attribute's presence to decide between [quote=X] and [quote], and
          // an empty one would write a header nobody asked for.
          return typeof author === "string" && author.length > 0
            ? { "data-bb-author": author }
            : {};
        },
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
      // Fallback - parse any blockquote (ignore author if present)
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
