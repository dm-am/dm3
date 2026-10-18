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
import {
  Spoiler,
  Nsfw,
  ModBlock,
  BbTab,
  BbQuote,
  Private,
  Noparse,
  BbMention,
} from "./index";
import { createEditorExtensions } from "../editorExtensions";

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
  it("should have correct name", () => {
    expect(Spoiler.name).toBe("spoiler");
  });

  it("should be a block node", () => {
    const editor = createTestEditor([Spoiler]);
    const schema = editor.schema;
    expect(schema.nodes.spoiler).toBeDefined();
    expect(schema.nodes.spoiler.spec.group).toBe("block");
  });

  it("should have data-bb-tag attribute", () => {
    const editor = createTestEditor([Spoiler]);
    editor.commands.setContent('<div data-bb-tag="spoiler"><p>test</p></div>');
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="spoiler"');
  });

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

  it("should provide toggle command", () => {
    const editor = createTestEditor([Spoiler]);
    expect(editor.commands.toggleSpoiler).toBeDefined();
  });
});

describe("NSFW Extension", () => {
  it("should have correct name", () => {
    expect(Nsfw.name).toBe("nsfw");
  });

  it("should be a block node", () => {
    const editor = createTestEditor([Nsfw]);
    const schema = editor.schema;
    expect(schema.nodes.nsfw).toBeDefined();
    expect(schema.nodes.nsfw.spec.group).toBe("block");
  });

  it("should have data-bb-tag attribute", () => {
    const editor = createTestEditor([Nsfw]);
    editor.commands.setContent('<div data-bb-tag="nsfw"><p>test</p></div>');
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="nsfw"');
  });

  it("should parse bb-nsfw class", () => {
    const editor = createTestEditor([Nsfw]);
    editor.commands.setContent('<div class="bb-nsfw"><p>test</p></div>');
    const json = editor.getJSON();
    expect(json.content?.some((n) => n.type === "nsfw")).toBe(true);
  });

  it("should provide toggle command", () => {
    const editor = createTestEditor([Nsfw]);
    expect(editor.commands.toggleNsfw).toBeDefined();
  });
});

describe("ModBlock Extension", () => {
  it("should have correct name", () => {
    expect(ModBlock.name).toBe("modBlock");
  });

  it("should be a block node", () => {
    const editor = createTestEditor([ModBlock]);
    const schema = editor.schema;
    expect(schema.nodes.modBlock).toBeDefined();
    expect(schema.nodes.modBlock.spec.group).toBe("block");
  });

  it("should have data-bb-tag attribute", () => {
    const editor = createTestEditor([ModBlock]);
    editor.commands.setContent('<div data-bb-tag="mod"><p>test</p></div>');
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="mod"');
  });

  it("should provide toggle command", () => {
    const editor = createTestEditor([ModBlock]);
    expect(editor.commands.toggleModBlock).toBeDefined();
  });
});

describe("BbTab Extension", () => {
  it("should have correct name", () => {
    expect(BbTab.name).toBe("bbTab");
  });

  it("should be an inline node", () => {
    const editor = createTestEditor([BbTab]);
    const schema = editor.schema;
    expect(schema.nodes.bbTab).toBeDefined();
    expect(schema.nodes.bbTab.spec.inline).toBe(true);
  });

  it("should have data-bb-tag attribute", () => {
    const editor = createTestEditor([BbTab]);
    editor.commands.setContent('<p><span data-bb-tag="tab"></span></p>');
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="tab"');
  });

  it("should provide insert command", () => {
    const editor = createTestEditor([BbTab]);
    expect(editor.commands.insertTab).toBeDefined();
  });
});

describe("BbQuote Extension", () => {
  it("should have correct name", () => {
    expect(BbQuote.name).toBe("bbQuote");
  });

  it("should be a block node", () => {
    const editor = createTestEditor([BbQuote]);
    const schema = editor.schema;
    expect(schema.nodes.bbQuote).toBeDefined();
    expect(schema.nodes.bbQuote.spec.group).toBe("block");
  });

  it("should have data-bb-tag attribute", () => {
    const editor = createTestEditor([BbQuote]);
    editor.commands.setContent(
      '<blockquote data-bb-tag="quote"><p>test</p></blockquote>',
    );
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="quote"');
  });

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

  it("should provide toggle command", () => {
    const editor = createTestEditor([BbQuote]);
    expect(editor.commands.toggleQuote).toBeDefined();
  });
});

describe("Private Extension", () => {
  it("should have correct name", () => {
    expect(Private.name).toBe("private");
  });

  it("should be a mark (inline)", () => {
    const editor = createTestEditor([Private]);
    const schema = editor.schema;
    expect(schema.marks.private).toBeDefined();
  });

  it("should have data-bb-tag attribute", () => {
    const editor = createTestEditor([Private]);
    editor.commands.setContent(
      '<p><span data-bb-tag="private" data-bb-character="CharacterName">secret</span></p>',
    );
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="private"');
  });

  it("should support character attribute", () => {
    const editor = createTestEditor([Private]);
    editor.commands.setContent(
      '<p><span data-bb-tag="private" data-bb-character="John Smith">secret</span></p>',
    );
    const html = editor.getHTML();
    expect(html).toContain('data-bb-character="John Smith"');
  });

  it("should provide toggle command", () => {
    const editor = createTestEditor([Private]);
    expect(editor.commands.togglePrivate).toBeDefined();
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

describe("Noparse Extension", () => {
  it("should have correct name", () => {
    expect(Noparse.name).toBe("noparse");
  });

  it("should be a mark (inline)", () => {
    const editor = createTestEditor([Noparse]);
    const schema = editor.schema;
    expect(schema.marks.noparse).toBeDefined();
  });

  it("should have data-bb-tag attribute", () => {
    const editor = createTestEditor([Noparse]);
    editor.commands.setContent(
      '<p><span data-bb-tag="noparse">[b]not bold[/b]</span></p>',
    );
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="noparse"');
  });

  it("should provide toggle command", () => {
    const editor = createTestEditor([Noparse]);
    expect(editor.commands.toggleNoparse).toBeDefined();
  });
});

describe("All BBCode Extensions Integration", () => {
  it("should work together without conflicts", () => {
    const editor = createTestEditor([
      Spoiler,
      Nsfw,
      ModBlock,
      BbTab,
      BbQuote,
      Private,
      Noparse,
    ]);

    // Each extension should be available
    expect(editor.schema.nodes.spoiler).toBeDefined();
    expect(editor.schema.nodes.nsfw).toBeDefined();
    expect(editor.schema.nodes.modBlock).toBeDefined();
    expect(editor.schema.nodes.bbTab).toBeDefined();
    expect(editor.schema.nodes.bbQuote).toBeDefined();
    expect(editor.schema.marks.private).toBeDefined();
    expect(editor.schema.marks.noparse).toBeDefined();
  });

  it("should handle nested structures", () => {
    const editor = createTestEditor([Spoiler, BbQuote]);

    // Spoiler inside quote
    editor.commands.setContent(`
      <blockquote data-bb-tag="quote">
        <div data-bb-tag="spoiler">
          <p>nested content</p>
        </div>
      </blockquote>
    `);

    const json = editor.getJSON();
    expect(json.content).toBeDefined();
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
    created.commands.setPrivate({ character: "Вася" });
    created.commands.setPrivate({ character: null });
    expect(created.getHTML()).toBe(
      '<p><span data-bb-tag="private" class="bb-private">секрет</span></p>',
    );
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
