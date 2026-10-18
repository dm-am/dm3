/**
 * Custom BBCode Image Extension for Tiptap.
 *
 * Renders [img width="300" height="200"]address[/img] in the unified shape
 * shared by the frontend bbcode converter, backend BbParserWrapper, and every
 * TruncatedContent consumer:
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
 *
 * The alt is always empty: an image has no alt in the markup (a reader never
 * sees one, and text nobody sees on the page is where an insult goes
 * unnoticed), and an alt that came with pasted HTML is not kept.
 * data-bb-source keeps a spelling of the tag the canonical one would change.
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
        width?: number | null;
        height?: number | null;
      }) => ReturnType;
    };
  }
}

/** A size of the frame, when the frame gives one. */
function sizeOf(frame: Element | null, name: string): number | null {
  const value = frame?.getAttribute(name);
  return value ? parseInt(value, 10) : null;
}

export const BbImage = Node.create<BbImageOptions>({
  name: "bbImage",

  addOptions() {
    return {
      inline: true,
      HTMLAttributes: {
        class: "bb-image",
        "data-bb-tag": "img",
        loading: "lazy",
        decoding: "async",
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
      // Each attribute reads its own element: a parse rule's getAttrs does
      // not have the last word, the attribute's parseHTML does.
      source: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-source"),
        renderHTML: (attributes) =>
          attributes.source === null
            ? {}
            : { "data-bb-source": attributes.source },
      },
      // Width and height live on the wrapper <span> of a sized image; the
      // bare <img> of a default one has neither.
      width: {
        default: null,
        parseHTML: (element) => sizeOf(element, "data-bb-width"),
        renderHTML: () => ({}),
      },
      height: {
        default: null,
        parseHTML: (element) => sizeOf(element, "data-bb-height"),
        renderHTML: () => ({}),
      },
    };
  },

  parseHTML() {
    return [
      // Wrapped form: <span class="bb-image-frame"><img class="bb-image"></span>
      // Tiptap passes the wrapper span to getAttrs; width/height come from
      // the span's data-bb-* attributes, the rest from the inner <img>.
      {
        tag: 'span[class~="bb-image-frame"]',
        getAttrs: (element) => {
          const span = element as HTMLElement;
          const img = span.querySelector("img");
          if (!img) return false;
          return {
            src: img.getAttribute("src"),
            source: img.getAttribute("data-bb-source"),
            width: sizeOf(span, "data-bb-width"),
            height: sizeOf(span, "data-bb-height"),
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
            source: el.getAttribute("data-bb-source"),
            width: null,
            height: null,
          };
        },
      },
      // Any other <img src="..."> is taken as a plain image
      {
        tag: "img[src]",
        getAttrs: (element) => {
          const el = element as HTMLElement;
          return {
            src: el.getAttribute("src"),
            source: null,
            width: null,
            height: null,
          };
        },
      },
    ];
  },

  renderHTML({ HTMLAttributes, node }) {
    const imgAttrs = mergeAttributes(
      this.options.HTMLAttributes,
      HTMLAttributes,
      { alt: "" },
    );

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
          // Only an address the server draws: http and https, and nothing on
          // the reader's own machine or network.
          const safeSrc = sanitizeImageUrl(attributes.src);
          if (safeSrc === "#") {
            return false;
          }

          return commands.insertContent({
            type: this.name,
            attrs: {
              src: safeSrc,
              width: attributes.width ?? null,
              height: attributes.height ?? null,
            },
          });
        },
    };
  },
});
