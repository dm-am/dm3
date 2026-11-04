// The cleanup of pasted HTML (cleanPastedHtml) and what it leaves for htmlToBbcode.

import { describe, it, expect } from "vitest";
import { bbcodeToHtml } from "./bbcodeToHtml";
import { htmlToBbcode } from "./htmlToBbcode";
import { cleanPastedHtml } from "./bbcodePaste";

describe("cleanPastedHtml", () => {
  describe("Microsoft Word cleanup", () => {
    it("removes o:p tags", () => {
      const input = "<p>Hello<o:p>&nbsp;</o:p></p>";
      expect(cleanPastedHtml(input)).toBe("<p>Hello</p>");
    });

    it("removes Word namespaced tags", () => {
      const input = "<w:WordDocument>junk</w:WordDocument><p>Content</p>";
      expect(cleanPastedHtml(input)).toBe("<p>Content</p>");
    });

    it("removes conditional comments", () => {
      const input =
        "<!--[if gte mso 9]><xml>stuff</xml><![endif]--><p>Content</p>";
      expect(cleanPastedHtml(input)).toBe("<p>Content</p>");
    });

    it("removes all HTML comments", () => {
      const input = "<!-- comment --><p>Content</p>";
      expect(cleanPastedHtml(input)).toBe("<p>Content</p>");
    });
  });

  describe("style cleanup", () => {
    it("removes inline styles", () => {
      const input = '<p style="font-family: Arial; color: red;">Text</p>';
      expect(cleanPastedHtml(input)).toBe("<p>Text</p>");
    });

    it("removes class attributes", () => {
      const input = '<p class="MsoNormal">Text</p>';
      expect(cleanPastedHtml(input)).toBe("<p>Text</p>");
    });

    it("removes id attributes", () => {
      const input = '<p id="para1">Text</p>';
      expect(cleanPastedHtml(input)).toBe("<p>Text</p>");
    });

    it("removes style tags", () => {
      const input = "<style>.MsoNormal { color: red; }</style><p>Text</p>";
      expect(cleanPastedHtml(input)).toBe("<p>Text</p>");
    });
  });

  describe("script and meta cleanup", () => {
    it("removes script tags", () => {
      const input = "<script>alert(1)</script><p>Content</p>";
      expect(cleanPastedHtml(input)).toBe("<p>Content</p>");
    });

    it("removes meta tags", () => {
      const input = '<meta charset="utf-8"><p>Content</p>';
      expect(cleanPastedHtml(input)).toBe("<p>Content</p>");
    });

    it("removes xml declarations", () => {
      const input = '<?xml version="1.0"?><p>Content</p>';
      expect(cleanPastedHtml(input)).toBe("<p>Content</p>");
    });
  });

  describe("span and font cleanup", () => {
    it("removes font tags", () => {
      const input = '<font face="Arial" size="3">Text</font>';
      expect(cleanPastedHtml(input)).toBe("Text");
    });

    it("removes empty spans", () => {
      const input = "<p>Hello<span>  </span>World</p>";
      expect(cleanPastedHtml(input)).toBe("<p>HelloWorld</p>");
    });

    it("unwraps non-empty spans", () => {
      const input = '<p>Hello <span style="color:red">World</span></p>';
      expect(cleanPastedHtml(input)).toBe("<p>Hello World</p>");
    });
  });

  describe("whitespace normalization", () => {
    it("normalizes CRLF to LF", () => {
      const input = "Line1\r\nLine2";
      expect(cleanPastedHtml(input)).toBe("Line1\nLine2");
    });

    it("normalizes CR to LF", () => {
      const input = "Line1\rLine2";
      expect(cleanPastedHtml(input)).toBe("Line1\nLine2");
    });

    it("converts tabs to spaces", () => {
      const input = "Col1\tCol2";
      expect(cleanPastedHtml(input)).toBe("Col1 Col2");
    });

    it("collapses multiple spaces", () => {
      const input = "Hello    World";
      expect(cleanPastedHtml(input)).toBe("Hello World");
    });

    it("limits consecutive newlines to 2", () => {
      const input = "Para1\n\n\n\nPara2";
      expect(cleanPastedHtml(input)).toBe("Para1\n\nPara2");
    });

    it("removes zero-width characters", () => {
      const input = "Hello\u200BWorld";
      expect(cleanPastedHtml(input)).toBe("HelloWorld");
    });
  });

  describe("structure cleanup", () => {
    it("removes html, head, body tags", () => {
      const input =
        "<html><head><title>Test</title></head><body><p>Content</p></body></html>";
      expect(cleanPastedHtml(input)).toBe("<p>Content</p>");
    });

    it("converts divs to content with newline", () => {
      const input = "<div>Line1</div><div>Line2</div>";
      const result = cleanPastedHtml(input);
      expect(result).toContain("Line1");
      expect(result).toContain("Line2");
    });
  });

  describe("edge cases", () => {
    it("handles empty string", () => {
      expect(cleanPastedHtml("")).toBe("");
    });

    it("trims whitespace", () => {
      expect(cleanPastedHtml("  text  ")).toBe("text");
    });

    it("preserves basic formatting tags", () => {
      const input = "<strong>Bold</strong> and <em>Italic</em>";
      expect(cleanPastedHtml(input)).toBe(
        "<strong>Bold</strong> and <em>Italic</em>",
      );
    });

    it("preserves links", () => {
      const input = '<a href="https://example.com">Link</a>';
      expect(cleanPastedHtml(input)).toBe(
        '<a href="https://example.com">Link</a>',
      );
    });

    it("preserves images", () => {
      const input = '<img src="https://example.com/image.png" alt="Test">';
      expect(cleanPastedHtml(input)).toBe(
        '<img src="https://example.com/image.png" alt="Test">',
      );
    });
  });

  describe("integration with htmlToBbcode", () => {
    it("Word HTML converts to clean BBCode", () => {
      const wordHtml = `
        <html><head><style>.MsoNormal{}</style></head>
        <body>
          <p class="MsoNormal" style="margin:0"><b><span style="font-family:Arial">Bold text</span></b></p>
          <p class="MsoNormal"><i>Italic text</i></p>
        </body></html>
      `;
      const cleaned = cleanPastedHtml(wordHtml);
      const bbcode = htmlToBbcode(cleaned);
      expect(bbcode).toContain("[b]Bold text[/b]");
      expect(bbcode).toContain("[i]Italic text[/i]");
      expect(bbcode).not.toContain("MsoNormal");
      expect(bbcode).not.toContain("Arial");
    });

    it("Google Docs HTML converts cleanly", () => {
      const gdocsHtml = `
        <span style="font-size:11pt;font-family:Arial;">
          <strong>Bold</strong> text with <a href="https://example.com">a link</a>
        </span>
      `;
      const cleaned = cleanPastedHtml(gdocsHtml);
      const bbcode = htmlToBbcode(cleaned);
      expect(bbcode).toContain("[b]Bold[/b]");
      expect(bbcode).toContain(
        '[link text="a link"]https://example.com[/link]',
      );
      expect(bbcode).not.toContain("font-size");
    });
  });

  /**
   * The server, this converter and the TipTap node all emit image markup and
   * have drifted before: the attribute set is frozen, not the order.
   */
  describe("image attributes", () => {
    it("carries the attributes every emitter must agree on", () => {
      const html = bbcodeToHtml("[img]https://example.com/a.png[/img]");

      for (const attribute of [
        'class="bb-image"',
        'data-bb-tag="img"',
        'loading="lazy"',
        'decoding="async"',
        'referrerpolicy="no-referrer"',
      ]) {
        expect(html).toContain(attribute);
      }
    });
  });
});
