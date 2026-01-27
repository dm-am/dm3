/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { Editor } from "@tiptap/core";
import Document from "@tiptap/extension-document";
import Paragraph from "@tiptap/extension-paragraph";
import Text from "@tiptap/extension-text";
import { BbImage } from "./BbImage";

// Helper to create a minimal editor for testing
function createTestEditor() {
  return new Editor({
    extensions: [Document, Paragraph, Text, BbImage],
    content: "",
  });
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

  it("should preserve alt attribute", () => {
    const editor = createTestEditor();
    editor.commands.setContent(
      '<p><img src="https://example.com/img.png" alt="Test image" /></p>',
    );
    const json = editor.getJSON();
    const imgNode = json.content?.[0]?.content?.[0] as
      | { attrs?: { alt?: string } }
      | undefined;
    expect(imgNode?.attrs?.alt).toBe("Test image");
  });
});
