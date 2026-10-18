/**
 * Tiptap BbSpelling Extension
 *
 * What the converter writes on an element for the way back, kept on the marks
 * and nodes of the visual mode that have no attribute of their own for it.
 *
 * - data-bb-open and data-bb-close: a tag typed in another case, [B] or
 *   [/QUOTE]. htmlToBbcode gives it back as typed, but only when the element
 *   still carries it, and the visual mode used to drop both: every name in
 *   capitals came back small after a trip through it, while the BBCode mode
 *   kept them.
 * - data-bb-glue-before, data-bb-glue-after and data-bb-newline-after: a block
 *   the author wrote with no line break at its edge, and one the text ends on
 *   a line break after. The visual mode puts a block on lines of its own, and
 *   without the mark the way back put a line break where none was typed.
 *
 * Spoiler, BbLooseItem and Private keep their own spellings and are left out
 * of the first list. Global attributes reach marks as well as nodes, and the
 * hidden block is a mark.
 */

import { Extension } from "@tiptap/core";

/** An attribute read from and written to one data-* attribute, as a string. */
function kept(name: string) {
  return {
    default: null,
    parseHTML: (element: HTMLElement) => element.getAttribute(name),
    renderHTML: (attributes: Record<string, unknown>) => {
      const value = attributes[attributeKey(name)];
      return typeof value === "string" ? { [name]: value } : {};
    },
  };
}

/** A flag read from and written to one data-* attribute, "true" or absent. */
function flag(name: string) {
  return {
    default: false,
    parseHTML: (element: HTMLElement) => element.getAttribute(name) === "true",
    renderHTML: (attributes: Record<string, unknown>) =>
      attributes[attributeKey(name)] ? { [name]: "true" } : {},
  };
}

/** The key of the attribute on a node: data-bb-glue-after is glueAfter. */
function attributeKey(name: string): string {
  return name
    .replace(/^data-bb-/, "")
    .replace(/-([a-z])/g, (_match, letter: string) => letter.toUpperCase());
}

/** The marks and nodes whose opening and closing tag can be typed in any case. */
export const SPELLED_TYPES = [
  "bold",
  "italic",
  "underline",
  "strike",
  "code",
  "noparse",
  "bbQuote",
  "nsfw",
  "modBlock",
  "bulletList",
  "orderedList",
  "listItem",
  "bbLooseList",
] as const;

/**
 * The nodes the converter draws as a block of their own, and the hidden block,
 * which is a block when it holds one: its mark then stands on the blocks it
 * holds, and the glue on it is the glue of the whole of them.
 */
export const GLUED_TYPES = [
  "private",
  "spoiler",
  "nsfw",
  "bbQuote",
  "modBlock",
  "bulletList",
  "orderedList",
  "bbLooseItem",
  "bbLooseList",
] as const;

export const BbSpelling = Extension.create({
  name: "bbSpelling",

  addGlobalAttributes() {
    return [
      {
        types: [...SPELLED_TYPES],
        attributes: {
          open: kept("data-bb-open"),
          close: kept("data-bb-close"),
        },
      },
      {
        // [tab] has an opening and nothing else.
        types: ["bbTab"],
        attributes: { open: kept("data-bb-open") },
      },
      {
        types: [...GLUED_TYPES],
        attributes: {
          glueBefore: flag("data-bb-glue-before"),
          glueAfter: flag("data-bb-glue-after"),
          newlineAfter: flag("data-bb-newline-after"),
        },
      },
    ];
  },
});

export default BbSpelling;
