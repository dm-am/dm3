/**
 * @vitest-environment jsdom
 */

/**
 * The grammar table through the visual mode.
 *
 * bbcodeGrammar.spec.ts holds the converter to every row of the table
 * (bbcodeGrammar.cases.ts): bbcodeToHtml and htmlToBbcode give the input back
 * as typed. The visual mode puts one more step between them, and it is not the
 * converter's: the editor reads the HTML into its own document, by the rules
 * of its extensions, and writes it out again. Whatever that document has no
 * place for is lost there, and nothing in the converter's spec can see it. A
 * mark on code that allowed no other mark took the bold off "[code][b]x[/b]",
 * and a [noparse] that allowed no [code] around it took the [code] off the way
 * DM2 writes an example in a monospace font.
 *
 * So every row goes through the editor the page builds (createEditorExtensions,
 * EDITOR_PARSE_OPTIONS), in the context of its surface, and has to come back
 * as typed. What the document cannot hold is listed by name below with the
 * reason it is harmless; a row that differs and is not listed fails, and so
 * does a listed row that comes back whole, so the list cannot outlive what it
 * excuses.
 */

import { afterEach, describe, expect, it } from "vitest";
import fc from "fast-check";
import { Editor } from "@tiptap/core";
import { bbcodeToHtml } from "@/shared/lib/utils/bbcodeToHtml";
import { htmlToBbcode } from "@/shared/lib/utils/htmlToBbcode";
import type { BBCodeContext } from "@/shared/lib/utils/bbcodeTagSet";
import {
  BBCODE_GRAMMAR_CASES,
  type GrammarCase,
  type GrammarSurface,
} from "@/shared/lib/utils/bbcodeGrammar.cases";
import { inThePreview, type Run } from "@/shared/lib/testing/markupReading";
import {
  literal,
  markupRuns,
  markupSoup,
  markupValid,
} from "@/shared/lib/testing/markupGenerator";
import { asBothSidesRead } from "@/shared/lib/testing/markupCorpus";
import { readers } from "@/shared/lib/testing/postReaders";
import {
  createEditorExtensions,
  EDITOR_PARSE_OPTIONS,
} from "./editorExtensions";

// The surfaces of content. The hint, the description of a game tag, has no
// visual mode: its rows are the grammar table's and the preview's
// (bbcodeGrammar.spec.ts, hintMarkup.spec.ts), and none of them is here.
const CONTEXT_OF: Record<
  Exclude<GrammarSurface, "any" | "Hint">,
  BBCodeContext
> = {
  GamePost: "post",
  Comment: "common",
  GlobalChatMessage: "message",
  DirectMessage: "direct",
  Profile: "info",
};

function contextsOf(row: GrammarCase): BBCodeContext[] {
  if (row.surface === "Hint") return [];
  return row.surface === "any"
    ? Object.values(CONTEXT_OF)
    : [CONTEXT_OF[row.surface]];
}

const editors: Editor[] = [];

afterEach(() => {
  while (editors.length > 0) editors.pop()?.destroy();
});

/** The text as the visual mode gives it back. */
function throughTheVisualMode(input: string, context: BBCodeContext): string {
  const editor = new Editor({
    extensions: createEditorExtensions(),
    content: "",
  });
  editors.push(editor);
  editor.commands.setContent(bbcodeToHtml(input, { context }), {
    emitUpdate: false,
    parseOptions: EDITOR_PARSE_OPTIONS,
  });
  return htmlToBbcode(editor.getHTML());
}

const rows = BBCODE_GRAMMAR_CASES.filter(
  (row) => row.audience !== "author_edit",
).flatMap((row) =>
  contextsOf(row).map((context) => [row.id, context, row] as const),
);

/**
 * The rows the document of the visual mode cannot hold as typed, what each
 * comes back as, in every context of its row, and why that is no loss.
 */
const RESHAPED: Record<string, { back: string; why: string }> = {
  "e-end-of-text": {
    back: "текст ",
    why: "an empty tag holds no text, and a mark stands on text: the [private] left open at the end of the text has nothing in it to hide",
  },
  "p-code-empty": {
    back: "",
    why: "an empty tag holds no text, and a mark stands on text",
  },
  "p-noparse-empty": {
    back: "",
    why: "an empty tag holds no text, and a mark stands on text",
  },
  "e-inside-b": {
    back: '[private="Вася"][b]секрет[/b][/private]',
    why: "a hidden block is the outermost mark, and a tag written around one comes back inside it: the same bold text for the same readers",
  },
  "e-in-closed-code": {
    back: '[private="Вася"][code]пример[/code][/private]',
    why: "a hidden block is the outermost mark, and a tag written around one comes back inside it: the same monospace text for the same readers",
  },
  "o-control-in-code": {
    back: '[private="Вася"][code]пример[/code][/private]',
    why: "the same as e-in-closed-code, once the control character is taken out",
  },
  "n-stray-closing-sealed": {
    back: "[b]раз [/b][private][b]два[/b] три[/b][/private]",
    why: "a hidden block is the outermost mark, so the [b] around it comes back inside it too, and the [/b] typed inside the block, which closed nothing there, closes that [b]: the formatting inside the block changes for its readers, nothing leaves the block, and the BBCode mode names that [/b] (stray-closing)",
  },
  "e-broken-name-then-tag": {
    back: '[private [u]="Ва[ся"]секрет[/u][/private]',
    why: "a hidden block is the outermost mark, so the [u] comes back inside it, in front of the text that followed a spelling ended right after the name; a space keeps that spelling a hidden block, and without it the block's text reached every reader",
  },
  "e-erased-leaves-no-tag": {
    back: "Раз [private][b]x[/b][/private] два",
    why: "a hidden block is the outermost mark, and a tag written around one comes back inside it, as in e-inside-b: the same bold text for the same readers",
  },
  "e-erased-leaves-no-tag-around-spaces": {
    back: 'раз [i] [/i][private="Вася"][i]секрет[/i][/private][i] [/i] два',
    why: "a hidden block is the outermost mark, so the [i] around it comes back in three pieces, before, inside and after it: the same slanted text for the same readers",
  },
  "e-erased-keeps-typed-empty-tag": {
    back: "[private]секрет[/private]",
    why: "an empty tag holds no text, and a mark stands on text, as in p-code-empty",
  },
  "p-code-in-code": {
    back: "[code]раз два три[/code]",
    why: "a text carries one mark of a kind: [code] inside [code] adds nothing to the font",
  },
  "p-code-around-quote": {
    back: "[quote][code]цитата[/code][/quote]",
    why: "a mark stands on text and not on a block: the [code] around a quote comes back on the text inside it, which is the same monospace text",
  },
  "p-code-around-list": {
    back: "[ul][li][code]раз[/code][/li][li][code]два[/code][/li][/ul]",
    why: "a mark stands on text and not on a block: the [code] around a list comes back on the text of each item",
  },
  "p-code-around-spoiler": {
    back: "[spoiler][code]текст[/code][/spoiler][code] хвост[/code]",
    why: "a mark stands on text and not on a block: the [code] around a spoiler comes back on the text inside it and after it",
  },
};

describe("the grammar table through the visual mode", () => {
  it.each(rows)("%s in %s comes back as typed", (id, context, row) => {
    const back = throughTheVisualMode(row.input, context);
    const reshaped = RESHAPED[id];
    if (reshaped) {
      expect(back, "listed as reshaped, and came back as typed").not.toBe(
        asBothSidesRead(row.input),
      );
      expect(back, reshaped.why).toBe(reshaped.back);
    } else {
      expect(back).toBe(asBothSidesRead(row.input));
    }
  });

  it("lists only rows of the table, each with its reason", () => {
    const ids = new Set(rows.map(([id]) => id));
    for (const [id, { why }] of Object.entries(RESHAPED)) {
      expect(ids.has(id), id).toBe(true);
      expect(why.length, id).toBeGreaterThan(20);
    }
  });
});

// Generated texts: whatever the visual mode reshapes, every word keeps its order and looks on the preview.
// A failure names the smallest text and its seed (docs/guides/TESTING.md, "Генеративные проверки разметки").

/** A spoiler's caption loses the formatting a block tag written around it had: the one look a reshape may change. */
function captionsUnformatted(runs: Run[]): Run[] {
  const out: Run[] = [];
  for (const run of runs) {
    const marks = run.marks.split("|").includes("caption")
      ? run.marks
          .split("|")
          .filter((mark) => !["b", "i", "u", "strike", "code"].includes(mark))
          .join("|")
      : run.marks;
    const last = out[out.length - 1];
    if (last && last.marks === marks) last.text += run.text;
    else out.push({ text: run.text, marks });
  }
  return out;
}

/** The text as the visual mode gives it back, through one editor for a run. */
function visualMode(editor: Editor, text: string, context: BBCodeContext) {
  editor.commands.setContent(bbcodeToHtml(text, { context }), {
    emitUpdate: false,
    parseOptions: EDITOR_PARSE_OPTIONS,
  });
  return htmlToBbcode(editor.getHTML());
}

/**
 * What a run holds that a reader can take away: its words, and, for a thing
 * drawn whole (the caption of a spoiler, a link, a mention, an image), the
 * thing itself. The pieces of the alphabet give those no word of their own, so
 * without them a caption that went out of a hidden block went unseen.
 */
function whatARunHolds(run: Run): string[] {
  const words = run.text.match(/Q\d{4}Q/g) ?? [];
  const whole = run.marks
    .split("|")
    .some(
      (mark) =>
        mark === "caption" ||
        mark.startsWith("link:") ||
        mark.startsWith("mention:") ||
        mark.startsWith("img:"),
    );
  return whole ? [...words, `whole:${run.text}`] : words;
}

describe("generated texts through the visual mode", () => {
  // Markup the grammar accepts whole, the shape a text written in the visual
  // mode has: it shows what it showed, every word with the same formatting.
  it.each(Object.values(CONTEXT_OF))(
    "that the grammar accepts whole show what they showed, in %s",
    (context) => {
      const editor = new Editor({
        extensions: createEditorExtensions(),
        content: "",
      });
      editors.push(editor);
      fc.assert(
        fc.property(markupValid(context), (text) => {
          const back = visualMode(editor, text, context);
          const shown = captionsUnformatted(
            inThePreview(bbcodeToHtml(text, { context })),
          );
          const after = captionsUnformatted(
            inThePreview(bbcodeToHtml(back, { context })),
          );
          if (JSON.stringify(after) !== JSON.stringify(shown)) {
            throw new Error(
              `${literal(text)} in ${context} comes back from the visual mode as ${literal(back)}`,
            );
          }
        }),
        markupRuns(`visual mode, valid, in ${context}`, 80),
      );
    },
  );

  // Anything at all, broken tags, tags left open and closing tags with nothing
  // to close included. Such a text is text by where its pieces stand, and the
  // visual mode, which moves tags (RESHAPED), can move them into another
  // meaning; what it must never do is move a word across the edge of a hidden
  // block. So every reader of a game post reads the same words after it as
  // before, and the same captions, links, mentions and images.
  it("of any kind keep every word with the readers it had", () => {
    const editor = new Editor({
      extensions: createEditorExtensions(),
      content: "",
    });
    editors.push(editor);
    fc.assert(
      fc.property(markupSoup(), (text) => {
        const back = visualMode(editor, text, "post");
        const before = readers(
          inThePreview(bbcodeToHtml(text, { context: "post" })),
          whatARunHolds,
        );
        const after = readers(
          inThePreview(bbcodeToHtml(back, { context: "post" })),
          whatARunHolds,
        );
        if (JSON.stringify(after) !== JSON.stringify(before)) {
          throw new Error(
            `${literal(text)} comes back from the visual mode as ${literal(back)}, and ` +
              `its readers read ${JSON.stringify(after)} instead of ${JSON.stringify(before)}`,
          );
        }
      }),
      markupRuns("visual mode, readers", 250),
    );
  });
});
