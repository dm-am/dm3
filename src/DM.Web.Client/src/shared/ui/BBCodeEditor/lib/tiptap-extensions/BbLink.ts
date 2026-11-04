/**
 * Custom BBCode Link Extension for Tiptap
 *
 * [link]address[/link] shows the word "ссылка", as on DM2, and
 * [link text="Главная"]address[/link] shows its own text. The mark keeps what
 * the standard Link extension discards: data-bb-selfref for the first,
 * data-bb-text for the second, and data-bb-source for a spelling the canonical
 * one would change.
 */

import { Mark, mergeAttributes } from "@tiptap/core";
import { DEFAULT_LINK_TEXT } from "@/shared/lib/bbcode/bbcodeConstants";
import { sanitizeUrl } from "@/shared/lib/bbcode/bbcodeAddresses";
import { kept } from "./kept";

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    bbLink: {
      setBbLink: (attributes: { href: string; text?: string }) => ReturnType;
    };
  }
}

export const BbLink = Mark.create({
  name: "bbLink",

  priority: 1000,

  keepOnSplit: false,

  addAttributes() {
    return {
      // First: the drawn <a> keeps target and rel ahead of href.
      target: {
        default: "_blank",
      },
      rel: {
        default: "noopener",
      },
      href: {
        default: null,
      },
      // The text parameter: null when the link has none and shows the default
      // word, a string (empty included) when [link text="..."] gives one.
      text: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-text"),
        renderHTML: (attributes) =>
          attributes.text === null
            ? { "data-bb-selfref": "true" }
            : { "data-bb-text": attributes.text },
      },
      source: kept("data-bb-source"),
    };
  },

  parseHTML() {
    return [
      {
        tag: 'a[href][data-bb-tag="link"]',
        getAttrs: (element) => {
          const el = element as HTMLElement;
          let text: string | null;
          if (el.getAttribute("data-bb-selfref") === "true") text = null;
          else if (el.hasAttribute("data-bb-text")) {
            text = el.getAttribute("data-bb-text");
          } else {
            text = el.textContent === DEFAULT_LINK_TEXT ? null : el.textContent;
          }
          return {
            href: el.getAttribute("href"),
            text,
            source: el.getAttribute("data-bb-source"),
          };
        },
      },
      // Fallback for regular links
      {
        tag: "a[href]:not([data-bb-tag])",
        getAttrs: (element) => {
          const el = element as HTMLElement;
          const href = el.getAttribute("href");
          const text = el.textContent;
          // If text is the same as href or is default link text, treat as selfref
          if (text === href || text === DEFAULT_LINK_TEXT) {
            return { href, text: null };
          }
          return { href, text };
        },
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "a",
      mergeAttributes(HTMLAttributes, {
        "data-bb-tag": "link",
      }),
      0,
    ];
  },

  addCommands() {
    return {
      setBbLink:
        (attributes) =>
        ({ commands, state }) => {
          // Only an address the server shows: http and https, and nothing on
          // the reader's own machine or network.
          const safeHref = sanitizeUrl(attributes.href);
          if (safeHref === "#") {
            return false;
          }
          const text = attributes.text || null;

          const { from, to } = state.selection;
          const hasSelection = from !== to;

          if (hasSelection) {
            return commands.setMark(this.name, { href: safeHref, text });
          }
          return commands.insertContent({
            type: "text",
            text: text ?? DEFAULT_LINK_TEXT,
            marks: [{ type: this.name, attrs: { href: safeHref, text } }],
          });
        },
    };
  },
});
