/**
 * Tiptap Private Extension
 *
 * Renders [private="Вася, Петя"]content[/private] and the bare
 * [private]content[/private] as styled inline text. Uses data-bb-tag="private"
 * for lossless round-trip conversion: the addressees ride in
 * data-bb-character, absent for the bare block. A spelling the grammar does
 * not know is still a hidden block, addressed to nobody, and keeps what was
 * typed in data-bb-open; a closing tag typed in another case, [/PRIVATE], rides
 * in data-bb-close; a block its closing tag never ended says so in
 * data-bb-unclosed.
 */

import { Mark, isMarkActive, mergeAttributes } from "@tiptap/core";
import { kept } from "./kept";

interface PrivateAttributes {
  /** Addressee names as written, comma-separated; null for a bare block. */
  character: string | null;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    private: {
      togglePrivate: (attributes: PrivateAttributes) => ReturnType;
    };
  }
}

export const Private = Mark.create({
  name: "private",

  // The outermost mark: the block is written around everything it holds, and
  // a link that held the block instead wrote itself back without it. Above
  // every other mark (see the order of the marks in editorExtensions.ts).
  priority: 1100,

  // A block inside another stays a block inside another. With the default,
  // one private mark to a piece of text, the inner block took the text off
  // the outer one: `[private]раз [private="Вася"]два[/private][/private]` came
  // back as two blocks side by side, the save that refuses a block inside
  // another took it, and the owner of the character named in the inner block
  // read what only the readers of both blocks had read. Two marks of the kind
  // stand on one text when their attributes differ; the commands below put
  // one in place of another, as before.
  excludes: "",

  addAttributes() {
    return {
      character: {
        default: null,
        // data-bb-character is what the editor writes, present for an
        // addressed block and absent for a bare one.
        parseHTML: (element) => element.getAttribute("data-bb-character"),
        renderHTML: (attributes) =>
          attributes.character === null
            ? {}
            : { "data-bb-character": attributes.character },
      },
      open: kept("data-bb-open"),
      close: kept("data-bb-close"),
      unclosed: {
        default: false,
        parseHTML: (element) =>
          element.getAttribute("data-bb-unclosed") === "true",
        renderHTML: (attributes) =>
          attributes.unclosed ? { "data-bb-unclosed": "true" } : {},
      },
    };
  },

  // Every shape a span of private text comes in: a missed one published the text to the whole room.
  parseHTML() {
    return [
      {
        tag: 'span[data-bb-tag="private"]',
      },
      {
        tag: "span.bb-private",
      },
      {
        tag: "span.private-text",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "span",
      mergeAttributes({ "data-bb-tag": "private" }, HTMLAttributes, {
        class: "bb-private",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      togglePrivate:
        (attributes) =>
        ({ state, commands }) => {
          if (isMarkActive(state, this.name, { ...attributes })) {
            return commands.unsetMark(this.name);
          }
          commands.unsetMark(this.name);
          return commands.setMark(this.name, attributes);
        },
    };
  },
});
