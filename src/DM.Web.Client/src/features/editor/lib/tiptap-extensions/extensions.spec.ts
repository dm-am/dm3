/**
 * @vitest-environment jsdom
 */

/**
 * Tiptap BBCode Extensions Tests
 *
 * Tests for custom Tiptap extensions that handle BBCode-specific elements.
 * Verifies parseHTML, renderHTML, and command functionality.
 */

import { describe, it, expect } from "vitest";
import { Editor, type AnyExtension } from "@tiptap/core";
import Document from "@tiptap/extension-document";
import Paragraph from "@tiptap/extension-paragraph";
import Text from "@tiptap/extension-text";
import {
  Spoiler,
  Nsfw,
  ModBlock,
  BbTab,
  CutMarker,
  BbQuote,
  Private,
  Noparse,
} from "./index";

// Helper to create a minimal editor for testing
function createTestEditor(extensions: AnyExtension[] = []) {
  return new Editor({
    extensions: [Document, Paragraph, Text, ...extensions],
    content: "",
  });
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

describe("CutMarker Extension", () => {
  it("should have correct name", () => {
    expect(CutMarker.name).toBe("cutMarker");
  });

  it("should be an atom node", () => {
    const editor = createTestEditor([CutMarker]);
    const schema = editor.schema;
    expect(schema.nodes.cutMarker).toBeDefined();
    expect(schema.nodes.cutMarker.spec.atom).toBe(true);
  });

  it('should have data-bb-tag="cut" attribute', () => {
    const editor = createTestEditor([CutMarker]);
    editor.commands.setContent('<hr data-bb-tag="cut" />');
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="cut"');
  });

  it("should provide insert command", () => {
    const editor = createTestEditor([CutMarker]);
    expect(editor.commands.insertCut).toBeDefined();
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

  it("should ignore author attribute (not supported)", () => {
    const editor = createTestEditor([BbQuote]);
    // Author should be ignored when parsing
    editor.commands.setContent(
      '<blockquote data-bb-tag="quote" data-bb-author="John"><p>test</p></blockquote>',
    );
    const html = editor.getHTML();
    // Output should NOT contain author
    expect(html).not.toContain("data-bb-author");
    expect(html).not.toContain("John");
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
      CutMarker,
      BbQuote,
      Private,
      Noparse,
    ]);

    // Each extension should be available
    expect(editor.schema.nodes.spoiler).toBeDefined();
    expect(editor.schema.nodes.nsfw).toBeDefined();
    expect(editor.schema.nodes.modBlock).toBeDefined();
    expect(editor.schema.nodes.bbTab).toBeDefined();
    expect(editor.schema.nodes.cutMarker).toBeDefined();
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
