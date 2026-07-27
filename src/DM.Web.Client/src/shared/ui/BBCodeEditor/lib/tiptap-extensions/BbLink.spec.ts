/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach } from "vitest";
import { Editor } from "@tiptap/core";
import Document from "@tiptap/extension-document";
import Paragraph from "@tiptap/extension-paragraph";
import Text from "@tiptap/extension-text";
import { BbLink } from "./BbLink";

// Track created editors so they are destroyed after each test.
// An undestroyed editor leaves ProseMirror DOMObserver flush timers
// pending, which can fire after jsdom teardown and fail the run.
const editors: Editor[] = [];

afterEach(() => {
  while (editors.length > 0) {
    editors.pop()?.destroy();
  }
});

// Helper to create a minimal editor for testing
function createTestEditor() {
  const editor = new Editor({
    extensions: [Document, Paragraph, Text, BbLink],
    content: "",
  });
  editors.push(editor);
  return editor;
}

describe("BbLink Extension", () => {
  it("should have correct name", () => {
    expect(BbLink.name).toBe("bbLink");
  });

  it("should be a mark", () => {
    const editor = createTestEditor();
    expect(editor.schema.marks.bbLink).toBeDefined();
  });

  it('should parse links with data-bb-tag="link"', () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><a href="https://example.com" data-bb-tag="link">text</a></p>',
    );
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="link"');
    expect(html).toContain('href="https://example.com"');
  });

  it("should preserve text attribute for [link=text]URL[/link] format", () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><a href="https://example.com" data-bb-tag="link" data-bb-text="Click here">Click here</a></p>',
    );
    const html = editor.getHTML();
    // The text should be preserved in data-bb-text attribute
    expect(html).toContain('data-bb-text="Click here"');
  });

  it("should mark selfref links with data-bb-selfref", () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><a href="https://example.com" data-bb-tag="link" data-bb-selfref="true">ссылка</a></p>',
    );
    const html = editor.getHTML();
    // Selfref should be rendered as data-bb-selfref
    expect(html).toContain('data-bb-selfref="true"');
  });

  it("should provide setBbLink command", () => {
    const editor = createTestEditor();
    expect(editor.commands.setBbLink).toBeDefined();
  });

  it("should provide unsetBbLink command", () => {
    const editor = createTestEditor();
    expect(editor.commands.unsetBbLink).toBeDefined();
  });

  it("should handle regular links as selfref if text equals URL", () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><a href="https://example.com">https://example.com</a></p>',
    );
    const json = editor.getJSON();
    const textNode = json.content?.[0]?.content?.[0];
    // Should be parsed as selfref (text = null)
    expect(textNode?.marks?.[0]?.attrs?.text).toBeNull();
  });

  it("should handle regular links as custom text if different from URL", () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><a href="https://example.com">Custom Text</a></p>',
    );
    const json = editor.getJSON();
    const textNode = json.content?.[0]?.content?.[0];
    // Should have text attribute
    expect(textNode?.marks?.[0]?.attrs?.text).toBe("Custom Text");
  });
});
