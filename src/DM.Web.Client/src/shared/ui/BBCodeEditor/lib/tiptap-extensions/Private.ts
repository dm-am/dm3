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

export interface PrivateOptions {
  HTMLAttributes: Record<string, unknown>;
}

export interface PrivateAttributes {
  /** Addressee names as written, comma-separated; null for a bare block. */
  character: string | null;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    private: {
      /**
       * Set a private mark
       */
      setPrivate: (attributes: PrivateAttributes) => ReturnType;
      /**
       * Toggle a private mark
       */
      togglePrivate: (attributes: PrivateAttributes) => ReturnType;
      /**
       * Unset a private mark
       */
      unsetPrivate: () => ReturnType;
    };
  }
}

export const Private = Mark.create<PrivateOptions>({
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

  addOptions() {
    return {
      HTMLAttributes: {},
    };
  },

  addAttributes() {
    return {
      "data-bb-tag": {
        default: "private",
        parseHTML: () => "private",
        renderHTML: () => ({ "data-bb-tag": "private" }),
      },
      character: {
        default: null,
        // data-bb-character is what the editor writes, present for an
        // addressed block and absent for a bare one. data-bb-addressees is
        // what the server writes on the AuthorEdit rendering, empty for a bare
        // block; without it the recipients were read as empty and the tag
        // came back addressed to nobody.
        parseHTML: (element) => {
          if (element.hasAttribute("data-bb-character")) {
            return element.getAttribute("data-bb-character");
          }
          if (element.hasAttribute("data-bb-addressees")) {
            return element.getAttribute("data-bb-addressees") || null;
          }
          return (
            element.getAttribute("data-bb-users") ??
            element.getAttribute("data-users")
          );
        },
        renderHTML: (attributes) =>
          attributes.character === null
            ? {}
            : { "data-bb-character": attributes.character },
      },
      open: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-open"),
        renderHTML: (attributes) =>
          attributes.open === null ? {} : { "data-bb-open": attributes.open },
      },
      close: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-close"),
        renderHTML: (attributes) =>
          attributes.close === null
            ? {}
            : { "data-bb-close": attributes.close },
      },
      unclosed: {
        default: false,
        parseHTML: (element) =>
          element.getAttribute("data-bb-unclosed") === "true",
        renderHTML: (attributes) =>
          attributes.unclosed ? { "data-bb-unclosed": "true" } : {},
      },
    };
  },

  // The server renders [private] as a span in the line. The div forms are what
  // it drew while the text was a block of its own, and they stay for text saved
  // then: matching only one shape meant the author's own edit view saw an
  // ordinary element, the mark was never applied, and saving published the
  // private text to the whole room.
  parseHTML() {
    return [
      {
        tag: 'div[data-bb-tag="private"]',
      },
      {
        tag: "div.private-message",
      },
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
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        class: "bb-private",
        "data-bb-tag": "private",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      // The block the author sets replaces the one the text had, and does
      // not go inside it.
      setPrivate:
        (attributes) =>
        ({ commands }) => {
          commands.unsetMark(this.name);
          return commands.setMark(this.name, attributes);
        },
      togglePrivate:
        (attributes) =>
        ({ state, commands }) => {
          if (isMarkActive(state, this.name, { ...attributes })) {
            return commands.unsetMark(this.name);
          }
          commands.unsetMark(this.name);
          return commands.setMark(this.name, attributes);
        },
      unsetPrivate:
        () =>
        ({ commands }) => {
          return commands.unsetMark(this.name);
        },
    };
  },
});

export default Private;
