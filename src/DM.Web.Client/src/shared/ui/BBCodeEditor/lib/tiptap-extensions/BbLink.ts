/**
 * Custom BBCode Link Extension for Tiptap
 *
 * Preserves data-bb-selfref and data-bb-text attributes that the standard
 * Link extension discards during HTML parsing.
 */

import { Mark, mergeAttributes } from "@tiptap/core";
import { DEFAULT_LINK_TEXT } from "@/shared/lib/utils/bbcodeConstants";
import { sanitizeUrl } from "@/shared/lib/utils/bbcode";

export interface BbLinkOptions {
  HTMLAttributes: Record<string, any>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    bbLink: {
      setBbLink: (attributes: { href: string; text?: string }) => ReturnType;
      unsetBbLink: () => ReturnType;
    };
  }
}

export const BbLink = Mark.create<BbLinkOptions>({
  name: "bbLink",

  priority: 1000,

  keepOnSplit: false,

  addOptions() {
    return {
      HTMLAttributes: {
        target: "_blank",
        rel: "noopener",
      },
    };
  },

  addAttributes() {
    return {
      href: {
        default: null,
      },
      // Custom text for [link=text]URL[/link] format
      // If null, displays DEFAULT_LINK_TEXT (selfref link)
      text: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-text"),
        renderHTML: (attributes) => {
          if (!attributes.text) {
            return { "data-bb-selfref": "true" };
          }
          return { "data-bb-text": attributes.text };
        },
      },
      target: {
        default: "_blank",
      },
      rel: {
        default: "noopener",
      },
    };
  },

  parseHTML() {
    return [
      {
        tag: 'a[href][data-bb-tag="link"]',
        getAttrs: (element) => {
          const el = element as HTMLElement;
          const selfref = el.getAttribute("data-bb-selfref");
          const text = el.getAttribute("data-bb-text");
          return {
            href: el.getAttribute("href"),
            text: selfref === "true" ? null : text || el.textContent,
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
    // Extract text to exclude it from HTML attributes (it's stored in data-bb-text)
    const { text: _text, ...rest } = HTMLAttributes;
    return [
      "a",
      mergeAttributes(this.options.HTMLAttributes, rest, {
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
          // Sanitize URL to prevent javascript: and other dangerous protocols
          const safeHref = sanitizeUrl(attributes.href);
          if (safeHref === "#") {
            // Reject dangerous URLs
            return false;
          }

          const { from, to } = state.selection;
          const hasSelection = from !== to;

          if (hasSelection) {
            // If there's selected text, apply mark to it
            return commands.setMark(this.name, {
              ...attributes,
              href: safeHref,
            });
          } else {
            // No selection - insert default link text with the link mark
            const displayText = attributes.text || DEFAULT_LINK_TEXT;
            return commands.insertContent({
              type: "text",
              text: displayText,
              marks: [
                {
                  type: this.name,
                  attrs: {
                    href: safeHref,
                    text: attributes.text || null,
                  },
                },
              ],
            });
          }
        },
      unsetBbLink:
        () =>
        ({ chain }) => {
          return chain().unsetMark(this.name).run();
        },
    };
  },
});
