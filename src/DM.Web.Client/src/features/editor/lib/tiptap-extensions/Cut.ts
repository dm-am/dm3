/**
 * Tiptap Cut Extension
 *
 * Renders [cut] as a visual marker indicating content cut point.
 * Only supports standalone [cut] marker (no closing tag, no wrapped content).
 * In display contexts, [cut] is invisible but affects text truncation.
 * In WYSIWYG editor, it shows as a visual divider.
 *
 * Uses data-bb-tag="cut" for lossless round-trip conversion.
 */

import { Node, mergeAttributes } from "@tiptap/core";
import type { CommandProps } from "@tiptap/core";

export interface CutOptions {
  HTMLAttributes: Record<string, unknown>;
}

declare module "@tiptap/core" {
  interface Commands<ReturnType> {
    cutMarker: {
      /**
       * Insert a standalone cut marker
       */
      insertCut: () => ReturnType;
    };
  }
}

// Standalone cut marker (horizontal rule style in editor)
export const CutMarker = Node.create<CutOptions>({
  name: "cutMarker",

  addOptions() {
    return {
      HTMLAttributes: {},
    };
  },

  group: "block",

  atom: true,

  draggable: true,

  selectable: true,

  addAttributes() {
    return {
      "data-bb-tag": {
        default: "cut",
        parseHTML: () => "cut",
        renderHTML: () => ({ "data-bb-tag": "cut" }),
      },
    };
  },

  parseHTML() {
    return [
      {
        tag: 'hr[data-bb-tag="cut"]',
      },
      {
        tag: "hr.bb-cut-marker",
      },
      {
        tag: "hr.cut-marker",
      },
    ];
  },

  renderHTML({ HTMLAttributes }) {
    return [
      "hr",
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        class: "bb-cut-marker",
        "data-bb-tag": "cut",
      }),
    ];
  },

  addCommands() {
    return {
      insertCut:
        () =>
        ({ commands }: CommandProps) => {
          return commands.insertContent({
            type: this.name,
          });
        },
    };
  },
});

export default CutMarker;
