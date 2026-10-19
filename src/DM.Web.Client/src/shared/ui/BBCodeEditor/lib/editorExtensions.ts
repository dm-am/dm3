// The visual mode's extensions in one list: the editor and the specs build from it.

import type { AnyExtension, EditorOptions } from "@tiptap/core";
import StarterKit from "@tiptap/starter-kit";
import Code from "@tiptap/extension-code";
import Underline from "@tiptap/extension-underline";
import Placeholder from "@tiptap/extension-placeholder";
import {
  Spoiler,
  Nsfw,
  BbTab,
  BbQuote,
  BbLooseItem,
  BbLooseList,
  BbSpelling,
  Private,
  Noparse,
  BbLink,
  BbImage,
  BbMention,
  ModBlock,
} from "./tiptap-extensions";
import { cleanPastedHtml } from "@/shared/lib/utils/bbcodePaste";
import { isChord } from "./editorKeys";

/** Whitespace kept: by default the parser folds runs of spaces and drops a line's leading ones. */
export const EDITOR_PARSE_OPTIONS = { preserveWhitespace: true } as const;

/** Copied text puts one line break before a block: a line is a paragraph here, as in the text field. */
export const EDITOR_CORE_OPTIONS: EditorOptions["coreExtensionOptions"] = {
  clipboardTextSerializer: { blockSeparator: "\n" },
};

// [code] only changes the font, so it excludes only itself. Priority writes outermost first: private 1100,
// code 1050, formatting 1020, the link 1000 (it drops what stands inside it), noparse 50 (it holds text).
const CodeMark = Code.extend({ excludes: "code", priority: 1050 });

// The nodes that let [private] stand on their blocks, so a block inside a hidden one stays inside it.
const HOLDERS_OF_HIDDEN_BLOCKS: ReadonlySet<string> = new Set([
  "doc",
  "bulletList",
  "orderedList",
  "listItem",
  "spoiler",
  "nsfw",
  "bbQuote",
  "modBlock",
  "bbLooseItem",
  "bbLooseList",
]);

function holdingHiddenBlocks<T extends AnyExtension>(extension: T): T {
  return HOLDERS_OF_HIDDEN_BLOCKS.has(extension.name)
    ? (extension.extend({ marks: "private" }) as T)
    : extension;
}

// Above the link, which writes itself back from address and caption and drops what stands inside it.
const FORMATTING_PRIORITY = 1020;

const FORMATTING_MARKS: ReadonlySet<string> = new Set([
  "bold",
  "italic",
  "strike",
  "underline",
]);

function rankedAboveTheLink<T extends AnyExtension>(extension: T): T {
  return FORMATTING_MARKS.has(extension.name)
    ? (extension.extend({ priority: FORMATTING_PRIORITY }) as T)
    : extension;
}

const Kit = StarterKit.extend({
  addExtensions() {
    return (this.parent?.() ?? [])
      .map(holdingHiddenBlocks)
      .map(rankedAboveTheLink);
  },
});

export function createEditorExtensions(placeholder = ""): AnyExtension[] {
  return [
    Kit.configure({
      heading: false,
      horizontalRule: false,
      blockquote: false,
      codeBlock: false,
      // The kit's code mark excludes every other mark; CodeMark takes them.
      code: false,
      // Both collide with ours: a second underline, and a link whose bare a[href] rule claims BbLink's anchors.
      underline: false,
      link: false,
    }),
    CodeMark,
    rankedAboveTheLink(Underline),
    Placeholder.configure({ placeholder }),
    ...[Spoiler, Nsfw, BbQuote, BbLooseItem, BbLooseList, ModBlock].map(
      holdingHiddenBlocks,
    ),
    BbTab,
    BbSpelling,
    Private,
    Noparse,
    BbLink,
    BbImage,
    BbMention,
  ];
}

/**
 * The visual mode's own handling of keys and paste, for the editorProps
 * of useEditor. The editor's keys come before every extension's.
 */
export function visualEditorProps(on: {
  /** The form sends. */
  submit: () => void;
  /** Whether the surface reads [spoiler]. */
  spoilerOffered: () => boolean;
  /** The spoiler of the toolbar, on or off at the caret. */
  toggleSpoiler: () => void;
}): EditorOptions["editorProps"] {
  return {
    // Ctrl+Enter sends (the kit bound it to a break). Ctrl+Shift+S by key code: by letter a Russian layout fell to strike.
    handleKeyDown: (_view, event) => {
      if ((event.ctrlKey || event.metaKey) && event.key === "Enter") {
        event.preventDefault();
        on.submit();
        return true;
      }
      if (isChord(event, "s", true) && on.spoilerOffered()) {
        event.preventDefault();
        on.toggleSpoiler();
        return true;
      }
      return false;
    },
    transformPastedHTML: cleanPastedHtml,
  };
}
