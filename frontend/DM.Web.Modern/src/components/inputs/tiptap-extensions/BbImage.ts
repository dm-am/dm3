/**
 * Custom BBCode Image Extension for Tiptap
 *
 * Ensures images have proper data-bb-tag="img" attribute for round-trip conversion.
 * Supports [img]URL[/img] and [img=WxH]URL[/img] formats.
 */

import { Node, mergeAttributes } from "@tiptap/core";
import {
  DEFAULT_IMG_MAX_WIDTH,
  DEFAULT_IMG_MAX_HEIGHT,
} from "@/utils/bbcodeConstants";
import { sanitizeImageUrl } from "@/utils/bbcode";

export interface BbImageOptions {
  inline: boolean;
  HTMLAttributes: Record<string, any>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    bbImage: {
      setBbImage: (attributes: {
        src: string;
        alt?: string;
        width?: number;
        height?: number;
      }) => ReturnType;
    };
  }
}

export const BbImage = Node.create<BbImageOptions>({
  name: "bbImage",

  addOptions() {
    return {
      inline: true,
      HTMLAttributes: {
        class: "bb-image",
        referrerpolicy: "no-referrer",
      },
    };
  },

  inline() {
    return this.options.inline;
  },

  group() {
    return this.options.inline ? "inline" : "block";
  },

  draggable: true,

  addAttributes() {
    return {
      src: {
        default: null,
      },
      alt: {
        default: "",
      },
      width: {
        default: null,
        parseHTML: (element) => {
          const w = element.getAttribute("data-bb-width");
          return w ? parseInt(w, 10) : null;
        },
        renderHTML: (attributes) => {
          if (!attributes.width) return {};
          return { "data-bb-width": attributes.width };
        },
      },
      height: {
        default: null,
        parseHTML: (element) => {
          const h = element.getAttribute("data-bb-height");
          return h ? parseInt(h, 10) : null;
        },
        renderHTML: (attributes) => {
          if (!attributes.height) return {};
          return { "data-bb-height": attributes.height };
        },
      },
    };
  },

  parseHTML() {
    return [
      {
        tag: 'img[data-bb-tag="img"]',
        getAttrs: (element) => {
          const el = element as HTMLElement;
          const w = el.getAttribute("data-bb-width");
          const h = el.getAttribute("data-bb-height");
          return {
            src: el.getAttribute("src"),
            alt: el.getAttribute("alt") || "",
            width: w ? parseInt(w, 10) : null,
            height: h ? parseInt(h, 10) : null,
          };
        },
      },
      {
        tag: "img[src]",
        getAttrs: (element) => {
          const el = element as HTMLElement;
          return {
            src: el.getAttribute("src"),
            alt: el.getAttribute("alt") || "",
            width: null,
            height: null,
          };
        },
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    const { width, height, ...rest } = HTMLAttributes;

    // Build style for dimensions
    let style = "";
    if (width && height) {
      style = `max-width:${width}px;max-height:${height}px`;
    } else if (width) {
      style = `max-width:${width}px`;
    } else if (height) {
      style = `max-height:${height}px`;
    } else {
      // Default max dimensions
      style = `max-width:${DEFAULT_IMG_MAX_WIDTH}px;max-height:${DEFAULT_IMG_MAX_HEIGHT}px`;
    }

    const attrs: Record<string, any> = {
      "data-bb-tag": "img",
      style,
    };

    if (width) attrs["data-bb-width"] = width;
    if (height) attrs["data-bb-height"] = height;

    return ["img", mergeAttributes(this.options.HTMLAttributes, rest, attrs)];
  },

  addCommands() {
    return {
      setBbImage:
        (attributes) =>
        ({ commands }) => {
          // Sanitize URL to prevent javascript: and other dangerous protocols
          const safeSrc = sanitizeImageUrl(attributes.src);
          if (safeSrc === "#") {
            // Reject dangerous URLs
            return false;
          }

          return commands.insertContent({
            type: this.name,
            attrs: { ...attributes, src: safeSrc },
          });
        },
    };
  },
});
