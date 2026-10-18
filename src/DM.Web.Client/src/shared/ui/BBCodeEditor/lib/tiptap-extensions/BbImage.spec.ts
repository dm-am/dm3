/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach } from "vitest";
import { Editor } from "@tiptap/core";
import Document from "@tiptap/extension-document";
import Paragraph from "@tiptap/extension-paragraph";
import Text from "@tiptap/extension-text";
import { BbImage } from "./BbImage";

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
    extensions: [Document, Paragraph, Text, BbImage],
    content: "",
  });
  editors.push(editor);
  return editor;
}

describe("BbImage Extension", () => {
  it("should have correct name", () => {
    expect(BbImage.name).toBe("bbImage");
  });

  it("should be a node", () => {
    const editor = createTestEditor();
    expect(editor.schema.nodes.bbImage).toBeDefined();
  });

  it("should be inline by default", () => {
    const editor = createTestEditor();
    expect(editor.schema.nodes.bbImage.spec.inline).toBe(true);
  });

  it('should parse images with data-bb-tag="img"', () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><img src="https://example.com/img.png" data-bb-tag="img" /></p>',
    );
    const html = editor.getHTML();
    expect(html).toContain('data-bb-tag="img"');
    expect(html).toContain('src="https://example.com/img.png"');
  });

  it("should parse regular images", () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><img src="https://example.com/img.png" /></p>',
    );
    const html = editor.getHTML();
    // Should add data-bb-tag when rendering
    expect(html).toContain('data-bb-tag="img"');
  });

  it("should add bb-image class", () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><img src="https://example.com/img.png" /></p>',
    );
    const html = editor.getHTML();
    expect(html).toContain('class="bb-image"');
  });

  it("should provide setBbImage command", () => {
    const editor = createTestEditor();
    expect(editor.commands.setBbImage).toBeDefined();
  });

  // An image has no alt: a reader never sees one, and text nobody sees on
  // the page is where an insult goes unnoticed. The editor keeps none, from
  // pasted HTML either, and draws the image with an empty one.
  it.each([
    '<p><img src="https://example.com/img.png" alt="Test image" /></p>',
    '<p><img src="https://example.com/img.png" data-bb-tag="img" alt="кот" data-alt="кот" /></p>',
    '<p><span class="bb-image-frame" data-bb-width="300"><img src="https://example.com/img.png" data-bb-tag="img" alt="кот" /></span></p>',
  ])("keeps no alt: %s", (html) => {
    const editor = createTestEditor();
    editor.commands.setContent(html);
    const out = editor.getHTML();
    expect(out).toContain('alt=""');
    expect(out).not.toContain("кот");
    expect(out).not.toContain("Test image");
    expect(out).not.toContain("data-alt");
  });
});
