/**
 * Custom BBCode Image Extension for Tiptap.
 *
 * Renders images in the unified shape shared by the frontend bbcode
 * parser, backend BbParserWrapper, and every TruncatedContent consumer:
 *
 *   - Default size: <img class="bb-image" data-bb-tag="img" src="..." alt="">
 *   - Custom size:  <span class="bb-image-frame" data-bb-width="W" data-bb-height="H"
 *                         style="--bb-image-max-width:Wpx;--bb-image-max-height:Hpx">
 *                     <img class="bb-image" data-bb-tag="img" src="..." alt="">
 *                   </span>
 *
 * Sizing flows through CSS custom properties declared on the wrapper span
 * (see _BbcodeContent.sass image contract). No inline max-width/max-height
 * on the <img> — this lets ancestors like TruncatedContent override image
 * sizing via normal class rules without !important.
 */

import { Node, mergeAttributes } from "@tiptap/core";
import { sanitizeImageUrl } from "@/shared/lib/utils/bbcode";

export interface BbImageOptions {
  inline: boolean;
  HTMLAttributes: Record<string, unknown>;
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
        "data-bb-tag": "img",
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
        // Width lives on the wrapper <span> for custom-sized images. For
        // bare default-size <img> Tiptap falls back to null via getAttrs.
        parseHTML: (element) => {
          const w = element.getAttribute("data-bb-width");
          return w ? parseInt(w, 10) : null;
        },
        renderHTML: () => ({}),
      },
      height: {
        default: null,
        parseHTML: (element) => {
          const h = element.getAttribute("data-bb-height");
          return h ? parseInt(h, 10) : null;
        },
        renderHTML: () => ({}),
      },
    };
  },

  parseHTML() {
    return [
      // Wrapped form: <span class="bb-image-frame"><img class="bb-image"></span>
      // Tiptap passes the wrapper span to getAttrs; width/height come from
      // the span's data-bb-* attributes, src/alt come from the inner <img>.
      {
        tag: 'span[class~="bb-image-frame"]',
        getAttrs: (element) => {
          const span = element as HTMLElement;
          const img = span.querySelector("img");
          if (!img) return false;
          const w = span.getAttribute("data-bb-width");
          const h = span.getAttribute("data-bb-height");
          return {
            src: img.getAttribute("src"),
            alt: img.getAttribute("alt") || "",
            width: w ? parseInt(w, 10) : null,
            height: h ? parseInt(h, 10) : null,
          };
        },
      },
      // Bare form: <img class="bb-image" data-bb-tag="img">
      {
        tag: 'img[data-bb-tag="img"]',
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
      // Legacy fallback — any other <img src="..."> is accepted without size
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

  renderHTML({ HTMLAttributes, node }) {
    const { width, height, ...rest } = HTMLAttributes as Record<
      string,
      unknown
    >;
    const imgAttrs = mergeAttributes(this.options.HTMLAttributes, rest);

    // Use the node's own attributes for width/height rather than the merged
    // HTMLAttributes (which strip unknown keys).
    const w = node.attrs.width as number | null;
    const h = node.attrs.height as number | null;

    if (w == null && h == null) {
      return ["img", imgAttrs];
    }

    // Wrapped form — CSS custom properties on the span carry the size.
    const cssVars: string[] = [];
    const spanAttrs: Record<string, string> = { class: "bb-image-frame" };
    if (w != null) {
      cssVars.push(`--bb-image-max-width:${w}px`);
      spanAttrs["data-bb-width"] = String(w);
    }
    if (h != null) {
      cssVars.push(`--bb-image-max-height:${h}px`);
      spanAttrs["data-bb-height"] = String(h);
    }
    spanAttrs.style = cssVars.join(";");

    return ["span", spanAttrs, ["img", imgAttrs]];
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
