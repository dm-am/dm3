/**
 * Tiptap Private Extension
 *
 * Renders [private=CharacterName]content[/private] as styled inline text.
 * Uses data-bb-tag="private" and data-bb-character for lossless round-trip conversion.
 * The character parameter specifies which character can see this private message.
 */

import { Mark, mergeAttributes } from "@tiptap/core";

export interface PrivateOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    private: {
      /**
       * Set a private mark for a character
       */
      setPrivate: (attributes: { character: string }) => ReturnType;
      /**
       * Toggle a private mark for a character
       */
      togglePrivate: (attributes: { character: string }) => ReturnType;
      /**
       * Unset a private mark
       */
      unsetPrivate: () => ReturnType;
    };
  }
}

export const Private = Mark.create<PrivateOptions>({
  name: "private",

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
        default: "",
        // data-bb-addressees is what the server writes on the AuthorEdit
        // rendering. Without it the recipients were read as empty and the tag
        // came back as a bare [private], addressed to nobody.
        parseHTML: (element) =>
          element.getAttribute("data-bb-addressees") ||
          element.getAttribute("data-bb-character") ||
          element.getAttribute("data-bb-users") ||
          element.getAttribute("data-users"),
        renderHTML: (attributes) => {
          if (!attributes.character) {
            return {};
          }
          return { "data-bb-character": attributes.character };
        },
      },
    };
  },

  // The server renders [private] as a div, and has done since the block became
  // block-level; these span forms are what the editor used to emit. Matching
  // only the spans meant the author's own edit view saw an ordinary div, the
  // mark was never applied, and saving published the private text to the whole
  // room. The div forms come first because that is what arrives from the server
  // today; the spans stay for text already inside an open editor.
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
      setPrivate:
        (attributes) =>
        ({ commands }) => {
          return commands.setMark(this.name, attributes);
        },
      togglePrivate:
        (attributes) =>
        ({ commands }) => {
          return commands.toggleMark(this.name, attributes);
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
