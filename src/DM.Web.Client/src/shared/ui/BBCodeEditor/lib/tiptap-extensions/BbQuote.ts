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

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    bbQuote: {
      toggleQuote: () => ReturnType;
    };
  }
}

export const BbQuote = Node.create({
  name: "bbQuote",

  group: "block",

  content: "block+",

  defining: true,

  parseHTML() {
    return [{ tag: "blockquote" }];
  },

  renderHTML({ HTMLAttributes }) {
    const attrs = mergeAttributes({ "data-bb-tag": "quote" }, HTMLAttributes, {
      class: "bb-quote",
    });

    return ["blockquote", attrs, 0];
  },

  addCommands() {
    return {
      toggleQuote:
        () =>
        ({ commands }) =>
          commands.toggleWrap(this.name),
    };
  },

  addKeyboardShortcuts() {
    return {
      "Mod-Shift-Q": () => this.editor.commands.toggleQuote(),
    };
  },
});
