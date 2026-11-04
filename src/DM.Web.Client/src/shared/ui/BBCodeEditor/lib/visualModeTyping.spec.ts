/**
 * @vitest-environment jsdom
 */

/**
 * Text typed in the visual mode.
 *
 * The texts of visualMode.spec.ts start from BBCode. The author of the visual
 * mode also types, and what he types is text on the screen whatever it spells:
 * a "[/private]" typed inside a hidden block stands inside the block, and a
 * "[noparse]" typed before a block is a word before it. Written out as typed,
 * the first ended the block there and gave the rest of it to every reader,
 * and the second, with a closing tag somewhere after the block, made the whole
 * block an example printed to every reader; the save took both texts. So what
 * a reader reads is compared with what the editor showed: no reader reads a
 * word the editor showed him no right to.
 */

import { afterEach, describe, expect, it } from "vitest";
import fc from "fast-check";
import { Editor } from "@tiptap/core";
import type { Node as ProseMirrorNode } from "@tiptap/pm/model";
import { bbcodeToHtml } from "@/shared/lib/utils/bbcodeToHtml";
import { htmlToBbcode } from "@/shared/lib/utils/htmlToBbcode";
import { validateBBCode } from "@/shared/lib/utils/bbcodeWarnings";
import {
  asBothSidesRead,
  refusesSave,
} from "@/shared/lib/testing/markupCorpus";
import { inThePreview, type Run } from "@/shared/lib/testing/markupReading";
import {
  literal,
  markupRuns,
  markupSoup,
  markupValid,
} from "@/shared/lib/testing/markupGenerator";
import { readers } from "@/shared/lib/testing/postReaders";
import {
  createEditorExtensions,
  EDITOR_PARSE_OPTIONS,
} from "./editorExtensions";

const editors: Editor[] = [];

afterEach(() => {
  while (editors.length > 0) editors.pop()?.destroy();
});

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
