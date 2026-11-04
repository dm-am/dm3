/**
 * @vitest-environment jsdom
 */

/**
 * Tiptap BBCode Extensions Tests
 *
 * Tests for custom Tiptap extensions that handle BBCode-specific elements.
 * Verifies parseHTML, renderHTML, and command functionality.
 */

import { describe, it, expect, afterEach } from "vitest";
import { Editor, type AnyExtension } from "@tiptap/core";
import Document from "@tiptap/extension-document";
import Paragraph from "@tiptap/extension-paragraph";
import Text from "@tiptap/extension-text";
import { Spoiler, Nsfw, BbQuote, Private, BbMention } from "./index";
import {
  createEditorExtensions,
  EDITOR_CORE_OPTIONS,
  visualEditorProps,
} from "../editorExtensions";
import { bbcodeToHtml } from "@/shared/lib/bbcode/bbcodeToHtml";

// Track created editors so they are destroyed after each test.
// An undestroyed editor leaves ProseMirror DOMObserver flush timers
// (20ms setTimeout) pending; if such a timer fires after the jsdom
// environment is torn down, the run fails with an unhandled
// "ReferenceError: document is not defined".
const editors: Editor[] = [];

afterEach(() => {
  while (editors.length > 0) {
    editors.pop()?.destroy();
  }
});

// Helper to create a minimal editor for testing
function createTestEditor(extensions: AnyExtension[] = []) {
  const editor = new Editor({
    extensions: [Document, Paragraph, Text, ...extensions],
    content: "",
  });
  editors.push(editor);
  return editor;
}

describe("Spoiler Extension", () => {
  it("should have collapsed attribute", () => {
    const editor = createTestEditor([Spoiler]);
    editor.commands.setContent(
      '<div data-bb-tag="spoiler" data-collapsed="true"><p>test</p></div>',
    );
    const json = editor.getJSON();
    const spoilerNode = json.content?.find((n) => n.type === "spoiler");
    expect(spoilerNode?.attrs?.collapsed).toBe(true);
  });

  it("should parse bb-spoiler class", () => {
    const editor = createTestEditor([Spoiler]);
    editor.commands.setContent('<div class="bb-spoiler"><p>test</p></div>');
    const json = editor.getJSON();
    expect(json.content?.some((n) => n.type === "spoiler")).toBe(true);
  });
});

describe("NSFW Extension", () => {
  it("should parse bb-nsfw class", () => {
    const editor = createTestEditor([Nsfw]);
    editor.commands.setContent('<div class="bb-nsfw"><p>test</p></div>');
    const json = editor.getJSON();
    expect(json.content?.some((n) => n.type === "nsfw")).toBe(true);
  });
});

describe("BbQuote Extension", () => {
  // The tag has no attribute, as in DM2: a block handed an author keeps the
  // text and loses the name, so nothing draws a line above it.
  it("drops an author attribute it is handed", () => {
    const editor = createTestEditor([BbQuote]);
    editor.commands.setContent(
      '<blockquote data-bb-tag="quote" data-bb-author="John"><p>test</p></blockquote>',
    );
    const html = editor.getHTML();
    expect(html).toBe(
      '<blockquote data-bb-tag="quote" class="bb-quote"><p>test</p></blockquote>',
    );
  });
});

describe("Private Extension", () => {
  it("should support character attribute", () => {
    const editor = createTestEditor([Private]);
    editor.commands.setContent(
      '<p><span data-bb-tag="private" data-bb-character="John Smith">secret</span></p>',
    );
    const html = editor.getHTML();
    expect(html).toContain('data-bb-character="John Smith"');
  });
});

// The visual mode read the anchor of a mention as text, and the next save
// turned "[mention]Вася[/mention]" into the words "@Вася".
describe("BbMention Extension", () => {
  it("keeps a mention one piece and gives back its markers", () => {
    const editor = createTestEditor([BbMention]);
    editor.commands.setContent(
      '<p>Привет, <a class="bb-mention" href="/users/%D0%92%D0%B0%D1%81%D1%8F" data-bb-tag="mention" data-bb-user="Вася">@Вася</a>!</p>',
    );
    expect(editor.schema.nodes.bbMention.spec.atom).toBe(true);
    expect(editor.getHTML()).toBe(
      '<p>Привет, <a class="bb-mention" href="/users/%D0%92%D0%B0%D1%81%D1%8F" data-bb-tag="mention" data-bb-user="Вася">@Вася</a>!</p>',
    );
  });
});

describe("Private: the bare block and a broken spelling", () => {
  it("renders no addressees for the bare block", () => {
    const editor = createTestEditor([Private]);
    editor.commands.setContent(
      '<p><span data-bb-tag="private">secret</span></p>',
    );
    expect(editor.getHTML()).not.toContain("data-bb-character");
  });

  it("keeps the spelling and the missing closing tag", () => {
    const editor = createTestEditor([Private]);
    editor.commands.setContent(
      '<p><span data-bb-tag="private" data-bb-open="[private=Вася]" data-bb-unclosed="true">secret</span></p>',
    );
    const html = editor.getHTML();
    expect(html).toContain('data-bb-open="[private=Вася]"');
    expect(html).toContain('data-bb-unclosed="true"');
  });
});

// A hidden block around a code sample or a noparse example has to stay on the
// text: the code mark of the kit and the noparse mark used to exclude every
// other mark, and a [private] written around either came off it in the visual
// mode and was published on the next save.
describe("The marks of the visual mode", () => {
  const editor = (): Editor => {
    const created = new Editor({ extensions: createEditorExtensions() });
    editors.push(created);
    return created;
  };

  it("let a hidden block stand on code and noparse text", () => {
    const { marks } = editor().schema;
    expect(marks.code.excludes(marks.private)).toBe(false);
    expect(marks.noparse.excludes(marks.private)).toBe(false);
    expect(marks.private.excludes(marks.code)).toBe(false);
    expect(marks.private.excludes(marks.noparse)).toBe(false);
  });

  // [code] changes the font and nothing else, and what [noparse] holds is
  // text: no mark is kept off either, and each is one mark of its kind.
  it("keep no other mark off code and noparse text", () => {
    const { marks } = editor().schema;
    const all = Object.values(marks);
    expect(all.length).toBeGreaterThanOrEqual(8);
    for (const mark of all) {
      expect([mark.name, marks.code.excludes(mark)]).toEqual([
        mark.name,
        mark.name === "code",
      ]);
      expect([mark.name, marks.noparse.excludes(mark)]).toEqual([
        mark.name,
        mark.name === "noparse",
      ]);
      expect([mark.name, mark.excludes(marks.code)]).toEqual([
        mark.name,
        mark.name === "code",
      ]);
    }
  });

  // The first mark is written outermost. A hidden block goes around all it
  // holds; [code] and the formatting around the link, which writes itself back
  // from its address and loses what stands inside it (the bold of
  // "[b][link]...[/link][/b]" did, found by the generated texts of
  // visualMode.spec.ts); [noparse] inside everything, so the other marks on
  // its text are written around the example.
  it("write the marks in the order the markup nests them", () => {
    const rank = Object.keys(editor().schema.marks);
    expect(rank[0]).toBe("private");
    expect(rank[1]).toBe("code");
    for (const formatting of ["bold", "italic", "underline", "strike"]) {
      expect(rank.indexOf("code"), formatting).toBeLessThan(
        rank.indexOf(formatting),
      );
      expect(rank.indexOf(formatting), formatting).toBeLessThan(
        rank.indexOf("bbLink"),
      );
    }
    expect(rank[rank.length - 1]).toBe("noparse");
  });

  // A block inside another is read by those who read both, and the save
  // refuses it. The visual mode used to put the inner block beside the outer
  // one, which the save takes, and the addressee of the inner block read it.
  it("hold a hidden block inside another", () => {
    const created = editor();
    created.commands.setContent(
      '<p><span data-bb-tag="private">раз <span data-bb-tag="private" data-bb-character="Вася">два</span></span></p>',
    );
    expect(created.getHTML()).toBe(
      '<p><span data-bb-tag="private" class="bb-private">раз <span data-bb-tag="private" data-bb-character="Вася" class="bb-private">два</span></span></p>',
    );
  });

  // The window of the hidden block addresses the text anew: the block it
  // makes takes the place of the one the text had, and does not go inside it.
  it("put a block the author sets in place of the one the text had", () => {
    const created = editor();
    created.commands.setContent(
      '<p><span data-bb-tag="private" data-bb-character="Вася">секрет</span></p>',
    );
    created.commands.selectAll();
    created.commands.togglePrivate({ character: "Петя" });
    expect(created.getHTML()).toBe(
      '<p><span data-bb-tag="private" data-bb-character="Петя" class="bb-private">секрет</span></p>',
    );
    created.commands.togglePrivate({ character: "Петя" });
    expect(created.getHTML()).toBe("<p>секрет</p>");
  });

  // A hidden block written around blocks stands on the blocks: the nodes that
  // hold blocks take the private mark on them, and no other mark. It used to
  // stand on the text inside them, and the blocks came back outside it.
  it("put a hidden block on the blocks it holds", () => {
    const { nodes, marks } = editor().schema;
    for (const holder of [
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
    ]) {
      expect([holder, nodes[holder].allowsMarkType(marks.private)]).toEqual([
        holder,
        true,
      ]);
      expect([holder, nodes[holder].allowsMarkType(marks.bold)]).toEqual([
        holder,
        false,
      ]);
    }
  });

  // A tag typed in another case, and a block with no line break at its edge,
  // are kept on the element for the way back (BbSpelling).
  it("keep the spelling of a tag and the glue of a block", () => {
    const created = editor();
    created.commands.setContent(
      '<blockquote data-bb-tag="quote" data-bb-open="[QUOTE]" data-bb-close="[/Quote]" data-bb-glue-after="true"><p><strong data-bb-tag="b" data-bb-open="[B]">раз</strong></p></blockquote><p>два</p>',
    );
    const html = created.getHTML();
    expect(html).toContain('data-bb-open="[QUOTE]"');
    expect(html).toContain('data-bb-close="[/Quote]"');
    expect(html).toContain('data-bb-glue-after="true"');
    expect(html).toContain('<strong data-bb-open="[B]">раз</strong>');
  });
});

describe("copied text", () => {
  // As the clipboard gets it from a selection of every word, first to last,
  // in the editor BBCodeEditor.vue builds.
  function copied(bbcode: string): string {
    const editor = new Editor({
      extensions: createEditorExtensions(),
      coreExtensionOptions: EDITOR_CORE_OPTIONS,
      editorProps: visualEditorProps({
        submit: () => {},
        spoilerOffered: () => true,
        toggleSpoiler: () => {},
      }),
      content: bbcodeToHtml(bbcode),
    });
    editors.push(editor);
    const words: number[] = [];
    editor.state.doc.descendants((node, pos) => {
      if (node.isText) words.push(pos, pos + node.nodeSize);
    });
    editor.commands.setTextSelection({
      from: words[0],
      to: words[words.length - 1],
    });
    const slice = editor.state.selection.content();
    return (
      editor.view.someProp("clipboardTextSerializer", (serialize) =>
        serialize(slice, editor.view),
      ) ?? ""
    );
  }

  it("keeps a line a line", () => {
    expect(copied("one\ntwo")).toBe("one\ntwo");
  });

  it("parts a line from the block under it by an empty line", () => {
    expect(copied("one\n[quote]two[/quote]")).toBe("one\n\ntwo");
  });
});
