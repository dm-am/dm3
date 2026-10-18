/**
 * The extensions of the visual mode, in one place: the editor builds its
 * document from them, and the specs that follow a text through the visual
 * mode build theirs from the same list rather than from a copy of it.
 */

import type { AnyExtension } from "@tiptap/core";
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

/**
 * How the visual mode reads the HTML bbcodeToHtml gives it.
 *
 * Whitespace is kept as it is: by default the parser folds a run of spaces
 * into one and drops the spaces that start a line, so "[code]" lost its
 * indentation and a line " ___" came back as "___" on the next save. A line
 * break inside a text becomes a hard break.
 */
export const EDITOR_PARSE_OPTIONS = { preserveWhitespace: true } as const;

/**
 * [code] changes the font and nothing else (BBCODE_RENDERING.md, "[code] и
 * [noparse]"), so its mark takes every other mark on its text, as [b] does:
 * the kit's own excludes every mark, and "[code][b]x[/b][/code]" lost its bold
 * in the visual mode. It excludes only itself.
 *
 * The order of the marks is the order they are written in, the first one
 * outermost, and a mark is ranked by its priority. [private] is first (1100):
 * the block is written around everything it holds. [code] comes next (1050),
 * then the formatting marks (1020, see FORMATTING_PRIORITY), both above the
 * link (1000), because a link writes itself back from its address and its
 * caption and loses whatever stands inside it. [noparse] is last (50): what it holds is
 * text, so nothing is ever written inside it, and every other mark on its text
 * is written around it, which is how DM2 writes an example in a monospace
 * font, "[code][noparse][b]x[/b][/noparse][/code]".
 */
const CodeMark = Code.extend({ excludes: "code", priority: 1050 });

/**
 * The nodes whose blocks a hidden block can stand on.
 *
 * A mark stands on text, and a [private] written around a block was put on the
 * text inside the block and not on the block: the block itself came back
 * outside the hidden one. `[private="Вася"][spoiler text="Письмо"]...` was
 * saved as a spoiler every reader saw, with its caption, around a hidden text,
 * and a [quote] with nothing in it came back as an empty frame on the page of
 * every reader. ProseMirror puts a mark on a block when the node holding the
 * block allows it, and these nodes allow [private] and nothing else: a block
 * inside a hidden block stays inside it, and the way back writes the hidden
 * block around the blocks it holds.
 */
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

/**
 * The formatting marks, [b], [i], [u] and [strike], ranked above the link and
 * below [code] (see CodeMark): a link writes itself back from its address and
 * its caption and loses whatever stands inside it, and at the default rank the
 * bold of "[b][link]...[/link][/b]" was written inside the link and dropped on
 * the way back. Found by the generated texts of visualMode.spec.ts.
 */
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

/**
 * The kit, with its own nodes that hold blocks taking hidden blocks and its
 * formatting marks ranked above the link.
 */
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
      heading: false, // BBCode doesn't support headings
      horizontalRule: false, // No BBCode tag draws a rule
      blockquote: false, // We use custom BbQuote instead
      codeBlock: false, // [code] is the inline code mark
      // The kit's code mark excludes every other mark; the one below takes
      // them, as [code] does.
      code: false,
      // The kit ships `underline` and `link` of its own, and both collide with
      // what this editor registers below.
      //
      // `underline` is the same extension twice under one name: tiptap answers
      // with "Duplicate extension names found: ['underline']" and keeps the
      // later registration. Turning the kit's copy off leaves exactly one.
      //
      // `link` is worse because the names differ, so nothing warns. Its parse
      // rule is a bare `a[href]` and it is registered ahead of BbLink, so every
      // anchor — including the `a[data-bb-tag="link"]` our own converter emits —
      // was claimed by it, and `data-bb-text` / `data-bb-selfref` were dropped
      // on the way in. That is the loss BbLink was written to prevent. Its
      // autolink and paste handlers were live too, quietly turning a typed URL
      // into a link the author never asked for.
      underline: false,
      link: false,
    }),
    CodeMark,
    rankedAboveTheLink(Underline),
    Placeholder.configure({ placeholder }),
    // BBCode custom extensions (aligned with DM2 server)
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
