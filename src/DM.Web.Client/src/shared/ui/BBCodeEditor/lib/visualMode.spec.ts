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
import type { Node as ProseMirrorNode } from "@tiptap/pm/model";
import {
  bbcodeToHtml,
  htmlToBbcode,
  validateBBCode,
} from "@/shared/lib/utils/bbcode";
import type { BBCodeContext } from "@/shared/lib/utils/bbcode";
import {
  BBCODE_GRAMMAR_CASES,
  type GrammarCase,
  type GrammarSurface,
} from "@/shared/lib/utils/bbcodeGrammar.cases";
import {
  asBothSidesRead,
  inThePreview,
  refusesSave,
  type Run,
  literal,
  markupRuns,
  markupSoup,
  markupValid,
} from "@/shared/lib/testing/markupFuzz";
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

/** The text less what both sides take out before reading it. */
function withoutRemoved(text: string): string {
  return Array.from(text)
    .filter((ch) => {
      const code = ch.charCodeAt(0);
      const control =
        (code <= 0x1f && ch !== "\t" && ch !== "\n" && ch !== "\r") ||
        (code >= 0x7f && code <= 0x9f);
      const reordering =
        (code >= 0x202a && code <= 0x202e) ||
        (code >= 0x2066 && code <= 0x2069);
      return !control && !reordering;
    })
    .join("");
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
        withoutRemoved(row.input),
      );
      expect(back, reshaped.why).toBe(reshaped.back);
    } else {
      expect(back).toBe(withoutRemoved(row.input));
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

/**
 * Texts nobody wrote down.
 *
 * The table holds the spellings someone thought of; texts generated from the
 * alphabet both sides share (markupAlphabet.ts, through markupFuzz.ts) find
 * the rest. Whatever the visual mode reshapes, what a text shows has to stay:
 * every word, in its order, drawn with the same things (bold, an item of a
 * list, a spoiler and its caption, a hidden block and its addressees), and so
 * read by the same readers. That is compared on the preview, which
 * bbcodeProperties.spec.ts holds to the page. Line breaks are not compared:
 * the visual mode has no line of its own between two blocks, and the reshapes
 * listed above move them.
 *
 * A failure names the smallest text it found and the seed to replay it with:
 * docs/guides/TESTING.md, section "Генеративные проверки разметки".
 */
/**
 * The runs with the formatting taken off the caption of a spoiler. A tag
 * written around a block comes back inside it (RESHAPED, e-inside-b and the
 * rows after it): the text of the block keeps its bold, and the caption on the
 * button, which stood inside the bold and now stands outside it, loses it. That
 * one change of looks is the price of the reshape, and it is the only one
 * forgiven here.
 */
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

/**
 * The words each reader of a game post reads in what a text shows, sorted: a
 * stranger, the leads, and the owners of "Вася" and "Петя". A hidden block with no
 * names is the leads' alone.
 *
 * @param held What a reader takes away from a run: its words by default.
 */
function readers(
  runs: Run[],
  held: (run: Run) => string[] = (run) => run.text.match(/Q\d{4}Q/g) ?? [],
): Record<string, string> {
  const read: Record<string, string[]> = {
    stranger: [],
    leads: [],
    Вася: [],
    Петя: [],
  };
  for (const run of runs) {
    const blocks = run.marks
      .split("|")
      .filter((mark) => mark.startsWith("private:"))
      .map((mark) =>
        mark
          .slice("private:".length)
          .split(",")
          .map((name) => name.toLowerCase()),
      );
    const words = held(run);
    read.leads.push(...words);
    if (blocks.length === 0) read.stranger.push(...words);
    for (const name of ["Вася", "Петя"]) {
      if (blocks.every((names) => names.includes(name.toLowerCase()))) {
        read[name].push(...words);
      }
    }
  }
  return Object.fromEntries(
    Object.entries(read).map(([reader, words]) => [
      reader,
      words.sort().join(" "),
    ]),
  );
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

/**
 * Text typed in the visual mode.
 *
 * Everything above starts from BBCode. The author of the visual mode also
 * types, and what he types is text on the screen whatever it spells: a
 * "[/private]" typed inside a hidden block stands inside the block, and a
 * "[noparse]" typed before a block is a word before it. Written out as typed,
 * the first ended the block there and gave the rest of it to every reader,
 * and the second, with a closing tag somewhere after the block, made the whole
 * block an example printed to every reader; the save took both texts. So what
 * a reader reads is compared with what the editor showed: no reader reads a
 * word the editor showed him no right to.
 */

/** The names of a hidden block, as the preview's reading writes them. */
function hiddenTo(character: string | null): string {
  const names = (character ?? "")
    .split(",")
    .map((name) => name.trim())
    .filter(Boolean);
  return `private:${names.join(",")}`;
}

/**
 * What the editor shows: every text of its document, with the hidden blocks
 * it stands in, whether the mark is on the text or on a block around it. The
 * characters the server takes out before reading are taken out here too, and
 * so is space, as the reading of the preview leaves it out (inThePreview): a
 * word they split on the screen is one word to every reader.
 */
function onTheScreen(editor: Editor): Run[] {
  const runs: Run[] = [];
  const walk = (node: ProseMirrorNode, marks: string[]): void => {
    const own = node.marks
      .filter((mark) => mark.type.name === "private")
      .map((mark) => hiddenTo(mark.attrs.character as string | null));
    const all = [...marks, ...own];
    if (node.isText) {
      runs.push({
        text: asBothSidesRead(node.text ?? "").replace(/\s/g, ""),
        marks: all.join("|"),
      });
      return;
    }
    node.forEach((child) => walk(child, all));
  };
  walk(editor.state.doc, []);
  return runs;
}

/** What each reader of a game post reads of the text the editor gives back. */
function readersOfTheSave(editor: Editor): Record<string, string> {
  return readers(
    inThePreview(
      bbcodeToHtml(htmlToBbcode(editor.getHTML()), { context: "post" }),
    ),
  );
}

/** The words a reader reads after that he was not shown, or "" for none. */
function gained(
  before: Record<string, string>,
  after: Record<string, string>,
): string {
  const found: string[] = [];
  for (const [reader, words] of Object.entries(after)) {
    const left = (before[reader] ?? "").split(" ").filter(Boolean);
    for (const word of words.split(" ").filter(Boolean)) {
      const at = left.indexOf(word);
      if (at >= 0) left.splice(at, 1);
      else found.push(`${reader}: ${word}`);
    }
  }
  return found.join(", ");
}

/** An editor holding a game post, read from BBCode as the page does. */
function editorWith(text: string): Editor {
  const editor = new Editor({
    extensions: createEditorExtensions(),
    content: "",
  });
  editors.push(editor);
  editor.commands.setContent(bbcodeToHtml(text, { context: "post" }), {
    emitUpdate: false,
    parseOptions: EDITOR_PARSE_OPTIONS,
  });
  return editor;
}

/** Where the text of the document goes on after a word of it. */
function after(editor: Editor, word: string): number {
  let found = -1;
  editor.state.doc.descendants((node, pos) => {
    if (found >= 0) return false;
    const at = node.isText ? (node.text ?? "").indexOf(word) : -1;
    if (at >= 0) found = pos + at + word.length;
    return true;
  });
  if (found < 0) throw new Error(`the document holds no "${word}"`);
  return found;
}

/** Type text where the caret stands, as a keyboard does: with its marks. */
function type(editor: Editor, at: number, text: string): void {
  editor.view.dispatch(editor.state.tr.insertText(text, at));
}

describe("text typed in the visual mode", () => {
  it("keeps a closing tag typed inside a hidden block inside it", () => {
    const editor = editorWith('[private="Вася"]Q0001Q Q0002Q[/private]');
    type(editor, after(editor, "Q0001Q"), " [/private] Q0003Q");

    expect(htmlToBbcode(editor.getHTML())).toBe(
      '[private="Вася"]Q0001Q [noparse][/private][/noparse] Q0003Q Q0002Q[/private]',
    );
    expect(readersOfTheSave(editor)).toEqual({
      stranger: "",
      leads: "Q0001Q Q0002Q Q0003Q",
      Вася: "Q0001Q Q0002Q Q0003Q",
      Петя: "",
    });
  });

  // The whole of what the review found: the typed opening tag after it starts
  // a block inside the block, which hides more and which the save refuses.
  it("gives no stranger the text between a typed closing and opening tag", () => {
    const editor = editorWith('[private="Вася"]Q0001Q Q0002Q[/private]');
    type(editor, after(editor, "Q0001Q"), " [/private] Q0003Q [private]");

    expect(readersOfTheSave(editor).stranger).toBe("");
    expect(gained(readers(onTheScreen(editor)), readersOfTheSave(editor))).toBe(
      "",
    );
  });

  it("keeps a closing tag typed inside an example inside the example", () => {
    const editor = editorWith(
      '[private="Вася"][noparse]Q0001Q Q0002Q[/noparse] Q0003Q[/private]',
    );
    type(editor, after(editor, "Q0001Q"), " [/noparse][/private] Q0004Q");

    expect(readersOfTheSave(editor)).toEqual({
      stranger: "",
      leads: "Q0001Q Q0002Q Q0003Q Q0004Q",
      Вася: "Q0001Q Q0002Q Q0003Q Q0004Q",
      Петя: "",
    });
  });

  it("keeps an example typed around a hidden block from taking the block", () => {
    const editor = editorWith('Q0001Q [private="Вася"]Q0002Q[/private] Q0003Q');
    type(editor, after(editor, "Q0003Q"), " [/noparse]");
    type(editor, after(editor, "Q0001Q"), " [noparse]");

    expect(readersOfTheSave(editor)).toEqual({
      stranger: "Q0001Q Q0003Q",
      leads: "Q0001Q Q0002Q Q0003Q",
      Вася: "Q0001Q Q0002Q Q0003Q",
      Петя: "Q0001Q Q0003Q",
    });
  });

  it("reads a pasted closing tag as the server does, without its control characters", () => {
    const editor = editorWith('[private="Вася"]Q0001Q Q0002Q[/private]');
    type(editor, after(editor, "Q0001Q"), " [/pri\u0001vate] Q0003Q");

    expect(readersOfTheSave(editor)).toEqual({
      stranger: "",
      leads: "Q0001Q Q0002Q Q0003Q",
      Вася: "Q0001Q Q0002Q Q0003Q",
      Петя: "",
    });
  });

  it("keeps a closing tag in the address of an image inside the hidden block", () => {
    const editor = editorWith('[private="Вася"]Q0001Q Q0002Q[/private]');
    editor.commands.insertContentAt(after(editor, "Q0001Q"), {
      type: "bbImage",
      attrs: { src: "https://example.com/[/private]" },
      marks: [{ type: "private", attrs: { character: "Вася" } }],
    });

    expect(readersOfTheSave(editor)).toEqual({
      stranger: "",
      leads: "Q0001Q Q0002Q",
      Вася: "Q0001Q Q0002Q",
      Петя: "",
    });
  });

  it("comes back as typed where what was typed ends no block", () => {
    const editor = editorWith('[private="Вася"]Q0001Q Q0002Q[/private]');
    type(editor, after(editor, "Q0001Q"), " [b]жирный[/b] [noparse]");

    expect(htmlToBbcode(editor.getHTML())).toBe(
      '[private="Вася"]Q0001Q [b]жирный[/b] [noparse] Q0002Q[/private]',
    );
  });

  // Any piece of the alphabet, typed anywhere in markup the grammar accepts,
  // once or twice: no reader of a game post reads after the save a word the
  // editor did not show him the right to read. Words may go the other way (a
  // typed "[private]" hides what follows it), which is safe and is the
  // author's own doing.
  it("never gives a reader a word the editor showed him no right to", () => {
    const editor = new Editor({
      extensions: createEditorExtensions(),
      content: "",
    });
    editors.push(editor);
    fc.assert(
      fc.property(
        markupValid("post"),
        fc.array(fc.tuple(markupSoup(), fc.nat()), {
          minLength: 1,
          maxLength: 2,
        }),
        (text, typings) => {
          editor.commands.setContent(bbcodeToHtml(text, { context: "post" }), {
            emitUpdate: false,
            parseOptions: EDITOR_PARSE_OPTIONS,
          });
          for (const [typed, where] of typings) {
            const places: number[] = [];
            editor.state.doc.descendants((node, pos) => {
              if (node.inlineContent) {
                for (let i = 0; i <= node.content.size; i++) {
                  places.push(pos + 1 + i);
                }
              }
              return true;
            });
            if (places.length === 0) return;
            type(editor, places[where % places.length], typed);
          }
          // A text the save refuses reaches no reader: a typed "[private]"
          // inside a block is a block inside a block, and a typed run of
          // "[quote]" nests past what the page builds.
          const back = htmlToBbcode(editor.getHTML());
          if (validateBBCode(back, "post").some(refusesSave)) return;
          const extra = gained(
            readers(onTheScreen(editor)),
            readersOfTheSave(editor),
          );
          if (extra) {
            throw new Error(
              `${literal(text)} with ${literal(JSON.stringify(typings))} typed in ` +
                `comes back as ${literal(htmlToBbcode(editor.getHTML()))}, and ${extra}`,
            );
          }
        },
      ),
      markupRuns("visual mode, typed text", 150),
    );
  });
});
