/**
 * @vitest-environment jsdom
 *
 * BBCode Round-Trip Integration Tests
 *
 * Tests the COMPLETE pipeline:
 * BBCode → bbcodeToHtml → Tiptap setContent → Tiptap getHTML → htmlToBbcode → BBCode
 *
 * This tests the exact scenario that's failing in production.
 */

import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { Editor } from "@tiptap/core";
import StarterKit from "@tiptap/starter-kit";
import Underline from "@tiptap/extension-underline";
import CodeBlock from "@tiptap/extension-code-block";
import { bbcodeToHtml, htmlToBbcode } from "./bbcode";
import {
  Spoiler,
  Nsfw,
  BbTab,
  BbQuote,
  Private,
  Noparse,
  BbLink,
  BbImage,
} from "@/features/editor/lib/tiptap-extensions";

// Create editor with the same configuration as BBCodeEditor.vue
function createProductionEditor() {
  return new Editor({
    extensions: [
      StarterKit.configure({
        heading: false,
        horizontalRule: false,
        blockquote: false,
      }),
      Underline,
      BbLink,
      BbImage,
      CodeBlock.configure({
        HTMLAttributes: {
          class: "bb-code",
          "data-bb-tag": "code",
        },
      }),
      Spoiler,
      Nsfw,
      BbTab,
      BbQuote,
      Private,
      Noparse,
    ],
    content: "",
  });
}

describe("BBCode Round-Trip Integration", () => {
  let editor: Editor;

  beforeEach(() => {
    editor = createProductionEditor();
  });

  afterEach(() => {
    editor.destroy();
  });

  // Helper function to trace the full pipeline
  function traceRoundTrip(inputBbcode: string) {
    console.log("\n=== ROUND-TRIP TRACE ===");
    console.log("1. Input BBCode:", JSON.stringify(inputBbcode));

    const html = bbcodeToHtml(inputBbcode);
    console.log("2. After bbcodeToHtml:", JSON.stringify(html));

    editor.commands.setContent(html, { emitUpdate: false });
    const editorHtml = editor.getHTML();
    console.log("3. After Tiptap getHTML:", JSON.stringify(editorHtml));

    const outputBbcode = htmlToBbcode(editorHtml);
    console.log("4. After htmlToBbcode:", JSON.stringify(outputBbcode));
    console.log("=== END TRACE ===\n");

    return { html, editorHtml, outputBbcode };
  }

  describe("THE FAILING CASE - Image and Link", () => {
    it("should preserve [img]URL[/img] with newlines and [link]URL[/link]", () => {
      const input = `[img]https://i.gyazo.com/72303c63ffdb7d4d21c9a2b8f6fff02b.jpg[/img]

[link]https://i.gyazo.com/72303c63ffdb7d4d21c9a2b8f6fff02b.jpg[/link]`;

      const { html, editorHtml, outputBbcode } = traceRoundTrip(input);

      // Verify each step
      expect(html).toContain("<img");
      expect(html).toContain('data-bb-tag="img"');
      expect(html).toContain(
        "https://i.gyazo.com/72303c63ffdb7d4d21c9a2b8f6fff02b.jpg",
      );

      // After Tiptap - should still have img
      expect(editorHtml).toContain("<img");

      // Final BBCode should have [img]...[/img]
      expect(outputBbcode).toContain("[img]");
      expect(outputBbcode).toContain("[/img]");
      expect(outputBbcode).toContain("[link]");
      expect(outputBbcode).toContain("[/link]");
    });
  });

  describe("Simple img tag", () => {
    it("should preserve [img]URL[/img]", () => {
      const input = "[img]https://example.com/image.png[/img]";
      const { outputBbcode } = traceRoundTrip(input);

      expect(outputBbcode).toBe("[img]https://example.com/image.png[/img]");
    });

    it("should handle img with surrounding text", () => {
      const input = "Before [img]https://example.com/image.png[/img] After";
      const { outputBbcode } = traceRoundTrip(input);

      expect(outputBbcode).toContain(
        "[img]https://example.com/image.png[/img]",
      );
    });
  });

  describe("Simple link tag", () => {
    it("should preserve [link]URL[/link]", () => {
      const input = "[link]https://example.com[/link]";
      const { outputBbcode } = traceRoundTrip(input);

      expect(outputBbcode).toContain("[link]");
      expect(outputBbcode).toContain("[/link]");
      expect(outputBbcode).toContain("https://example.com");
    });

    it("should preserve [link=text]URL[/link]", () => {
      const input = "[link=Click here]https://example.com[/link]";
      const { outputBbcode } = traceRoundTrip(input);

      expect(outputBbcode).toContain("[link=");
      expect(outputBbcode).toContain("[/link]");
    });
  });

  describe("Combined tags", () => {
    it("should preserve img followed by link", () => {
      const input =
        "[img]https://img.com/a.png[/img][link]https://link.com[/link]";
      const { outputBbcode } = traceRoundTrip(input);

      expect(outputBbcode).toContain("[img]");
      expect(outputBbcode).toContain("[/img]");
      expect(outputBbcode).toContain("[link]");
      expect(outputBbcode).toContain("[/link]");
    });

    it("should preserve img and link with newlines between", () => {
      const input =
        "[img]https://img.com/a.png[/img]\n\n[link]https://link.com[/link]";
      const { outputBbcode } = traceRoundTrip(input);

      expect(outputBbcode).toContain("[img]");
      expect(outputBbcode).toContain("[/img]");
      expect(outputBbcode).toContain("[link]");
      expect(outputBbcode).toContain("[/link]");
    });
  });

  describe("bbcodeToHtml output inspection", () => {
    it("should produce correct HTML for img", () => {
      const input = "[img]https://example.com/image.png[/img]";
      const html = bbcodeToHtml(input);

      console.log("HTML for img:", html);
      expect(html).toContain("<img");
      expect(html).toContain('src="https://example.com/image.png"');
      expect(html).toContain('data-bb-tag="img"');
    });

    it("should produce correct HTML for link", () => {
      const input = "[link]https://example.com[/link]";
      const html = bbcodeToHtml(input);

      console.log("HTML for link:", html);
      expect(html).toContain("<a");
      expect(html).toContain('href="https://example.com"');
      expect(html).toContain('data-bb-tag="link"');
      expect(html).toContain('data-bb-selfref="true"');
    });
  });

  describe("Tiptap HTML output inspection", () => {
    it("should render img with data-bb-tag", () => {
      const input =
        '<p><img src="https://example.com/image.png" data-bb-tag="img" class="bb-image" /></p>';
      editor.commands.setContent(input, { emitUpdate: false });
      const output = editor.getHTML();

      console.log("Tiptap output for img:", output);
      expect(output).toContain("<img");
      expect(output).toContain('data-bb-tag="img"');
    });
  });

  describe("htmlToBbcode output inspection", () => {
    it("should convert img HTML to BBCode", () => {
      const html =
        '<p><img src="https://example.com/image.png" data-bb-tag="img" class="bb-image" /></p>';
      const bbcode = htmlToBbcode(html);

      console.log("BBCode from img HTML:", bbcode);
      expect(bbcode).toBe("[img]https://example.com/image.png[/img]");
    });

    it("should convert img HTML without data-bb-tag to BBCode", () => {
      const html = '<p><img src="https://example.com/image.png" /></p>';
      const bbcode = htmlToBbcode(html);

      console.log("BBCode from unmarked img HTML:", bbcode);
      expect(bbcode).toBe("[img]https://example.com/image.png[/img]");
    });
  });
});
