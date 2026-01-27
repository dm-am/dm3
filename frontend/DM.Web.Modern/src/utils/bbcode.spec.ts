/**
 * BBCode Conversion Tests
 *
 * Tests lossless round-trip conversion between BBCode and HTML.
 * Verifies that BBCode → HTML → BBCode produces identical output.
 */

import { describe, it, expect } from "vitest";
import {
  bbcodeToHtml,
  htmlToBbcode,
  validateBBCode,
  bbcodeToPlainText,
  isTagAvailable,
  stripUnavailableTags,
  sanitizeUrl,
  sanitizeImageUrl,
  cleanPastedHtml,
  CONTEXT_TAGS,
  type BBCodeContext,
} from "./bbcode";

// ============================================================================
// HELPER FUNCTIONS
// ============================================================================

/**
 * Test that BBCode survives round-trip conversion
 */
function expectRoundTrip(bbcode: string) {
  const html = bbcodeToHtml(bbcode);
  const result = htmlToBbcode(html);
  expect(result).toBe(bbcode);
}

/**
 * Test that BBCode converts to expected HTML
 */
function expectToHtml(bbcode: string, expectedHtml: string) {
  expect(bbcodeToHtml(bbcode)).toBe(expectedHtml);
}

// ============================================================================
// BASIC FORMATTING TAGS
// ============================================================================

describe("BBCode Basic Formatting", () => {
  describe("[b] bold", () => {
    it("converts to HTML with marker", () => {
      expectToHtml(
        "[b]text[/b]",
        '<p><strong data-bb-tag="b">text</strong></p>',
      );
    });

    it("survives round-trip", () => {
      expectRoundTrip("[b]bold text[/b]");
    });

    it("handles nested content", () => {
      expectRoundTrip("[b]text with [i]italic[/i] inside[/b]");
    });
  });

  describe("[i] italic", () => {
    it("converts to HTML with marker", () => {
      expectToHtml("[i]text[/i]", '<p><em data-bb-tag="i">text</em></p>');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[i]italic text[/i]");
    });
  });

  describe("[u] underline", () => {
    it("converts to HTML with marker", () => {
      expectToHtml("[u]text[/u]", '<p><u data-bb-tag="u">text</u></p>');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[u]underlined text[/u]");
    });
  });

  describe("[strike] strikethrough (DM2 format)", () => {
    it("[strike] preserves as [strike]", () => {
      const html = bbcodeToHtml("[strike]deleted[/strike]");
      expect(html).toBe('<p><s data-bb-tag="strike">deleted</s></p>');
      expect(htmlToBbcode(html)).toBe("[strike]deleted[/strike]");
    });

    it("[strike] survives round-trip", () => {
      expectRoundTrip("[strike]deleted[/strike]");
    });
  });
});

// ============================================================================
// CODE AND PROTECTED BLOCKS
// ============================================================================

describe("BBCode Code Blocks", () => {
  describe("[code] block", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[code]const x = 1;[/code]");
      expect(html).toContain('data-bb-tag="code"');
      expect(html).toContain("const x = 1;");
    });

    it("preserves content exactly", () => {
      expectRoundTrip("[code]const x = 1;[/code]");
    });

    it("preserves special characters without escaping", () => {
      // Content should be escaped in HTML but restored in BBCode
      const bbcode = "[code]if (x < 10 && y > 5) { }[/code]";
      const html = bbcodeToHtml(bbcode);
      expect(html).toContain("&lt;");
      expect(html).toContain("&gt;");
      expect(html).toContain("&amp;&amp;");
      expectRoundTrip(bbcode);
    });

    it("preserves newlines inside code blocks", () => {
      const bbcode = "[code]line1\nline2\nline3[/code]";
      expectRoundTrip(bbcode);
    });

    it("preserves indentation", () => {
      const bbcode = "[code]function test() {\n  return true;\n}[/code]";
      expectRoundTrip(bbcode);
    });

    it("does not process BBCode tags inside code blocks", () => {
      const bbcode = "[code][b]not bold[/b] [i]not italic[/i][/code]";
      const html = bbcodeToHtml(bbcode);
      expect(html).not.toContain("<strong");
      expect(html).not.toContain("<em");
      expect(html).toContain("[b]not bold[/b]");
      expectRoundTrip(bbcode);
    });
  });
});

// ============================================================================
// LINKS
// ============================================================================

describe("BBCode Links (DM3 format)", () => {
  describe("[link=text]URL[/link] - custom text format", () => {
    it("converts to HTML with custom text displayed", () => {
      const bbcode = "[link=Click here]https://example.com[/link]";
      const html = bbcodeToHtml(bbcode);
      expect(html).toContain('data-bb-tag="link"');
      expect(html).toContain('data-bb-text="Click here"');
      expect(html).toContain(">Click here<");
      expect(html).toContain('href="https://example.com"');
    });

    it("survives round-trip", () => {
      const bbcode = "[link=Click here]https://example.com[/link]";
      const html = bbcodeToHtml(bbcode);
      expect(htmlToBbcode(html)).toBe(bbcode);
    });
  });

  describe('[link]URL[/link] - displays "ссылка"', () => {
    it('displays "ссылка" instead of URL', () => {
      const bbcode = "[link]https://example.com[/link]";
      const html = bbcodeToHtml(bbcode);
      expect(html).toContain('data-bb-selfref="true"');
      expect(html).toContain(">ссылка<");
      expect(html).toContain('href="https://example.com"');
    });

    it("survives round-trip", () => {
      const bbcode = "[link]https://example.com[/link]";
      const html = bbcodeToHtml(bbcode);
      expect(htmlToBbcode(html)).toBe(bbcode);
    });
  });

  describe("link attributes", () => {
    it('adds target="_blank" and rel="noopener"', () => {
      const html = bbcodeToHtml("[link=text]https://example.com[/link]");
      expect(html).toContain('target="_blank"');
      expect(html).toContain('rel="noopener"');
    });
  });
});

// ============================================================================
// LISTS
// ============================================================================

describe("BBCode Lists", () => {
  describe("[ul] unordered list", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[ul][li]item[/li][/ul]");
      expect(html).toContain('data-bb-tag="ul"');
      expect(html).toContain('data-bb-tag="li"');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[ul][li]item 1[/li][li]item 2[/li][/ul]");
    });
  });

  describe("[ol] ordered list", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[ol][li]item[/li][/ol]");
      expect(html).toContain('data-bb-tag="ol"');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[ol][li]first[/li][li]second[/li][/ol]");
    });
  });

  describe("nested lists", () => {
    it("handles nested unordered lists", () => {
      const bbcode = "[ul][li]item[ul][li]nested[/li][/ul][/li][/ul]";
      expectRoundTrip(bbcode);
    });
  });
});

// ============================================================================
// SPECIAL BLOCKS
// ============================================================================

describe("BBCode Special Blocks", () => {
  describe("[spoiler]", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[spoiler]hidden text[/spoiler]");
      expect(html).toContain('data-bb-tag="spoiler"');
      expect(html).toContain('class="bb-spoiler"');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[spoiler]secret content[/spoiler]");
    });
  });

  // NOTE: [spoiler=title] is NOT supported - only simple [spoiler] is available

  describe("[nsfw] (DM2 compatible)", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[nsfw]adult content[/nsfw]");
      expect(html).toContain('data-bb-tag="nsfw"');
      expect(html).toContain('class="bb-nsfw"');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[nsfw]hidden adult content[/nsfw]");
    });
  });

  describe("[noparse] (DM2 compatible)", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[noparse][b]not bold[/b][/noparse]");
      expect(html).toContain('data-bb-tag="noparse"');
      expect(html).toContain("[b]not bold[/b]");
      expect(html).not.toContain("<strong");
    });

    it("survives round-trip", () => {
      expectRoundTrip("[noparse][b]text[/b][/noparse]");
    });

    it("preserves BBCode tags inside without processing", () => {
      const bbcode = "[noparse][i][u][strike]test[/strike][/u][/i][/noparse]";
      const html = bbcodeToHtml(bbcode);
      expect(html).not.toContain("<em");
      expect(html).not.toMatch(/<u[^n]/); // Match <u but not <un (from <span...noparse)
      expect(html).not.toMatch(/<s[^p]/); // Match <s but not <sp (from <span)
      expectRoundTrip(bbcode);
    });
  });

  describe("[quote]", () => {
    it("basic quote survives round-trip", () => {
      expectRoundTrip("[quote]quoted text[/quote]");
    });

    it("[quote=X] author is stripped (not supported)", () => {
      // Author parameter is intentionally NOT supported
      const html = bbcodeToHtml("[quote=Username]quoted text[/quote]");
      const bbcode = htmlToBbcode(html);
      expect(bbcode).toBe("[quote]quoted text[/quote]");
      expect(bbcode).not.toContain("Username");
    });

    it("converts to HTML with proper structure", () => {
      const html = bbcodeToHtml("[quote]text[/quote]");
      expect(html).toContain('data-bb-tag="quote"');
      expect(html).not.toContain("data-bb-author");
    });
  });
});

// ============================================================================
// TAB AND CUT
// ============================================================================

describe("BBCode Tab and Cut", () => {
  describe("[tab]", () => {
    it("converts to non-breaking spaces with marker", () => {
      const html = bbcodeToHtml("[tab]");
      expect(html).toContain('data-bb-tag="tab"');
      expect(html).toContain("\u00A0"); // non-breaking space
    });

    it("survives round-trip", () => {
      expectRoundTrip("[tab]");
    });

    it("preserves multiple tabs", () => {
      expectRoundTrip("[tab][tab][tab]");
    });

    it("tabs in text survive round-trip", () => {
      expectRoundTrip("Hello[tab]World[tab]!");
    });
  });

  describe("[cut] - DM3 extension (standalone only)", () => {
    it("standalone [cut] converts to HR marker", () => {
      const html = bbcodeToHtml("[cut]");
      expect(html).toContain('data-bb-tag="cut"');
      expect(html).toContain("<hr");
      expect(html).toContain("bb-cut-marker");
    });

    it("standalone [cut] survives round-trip", () => {
      expectRoundTrip("[cut]");
    });

    it("[cut] is a truncation marker, not a block", () => {
      // [cut] does NOT have a closing tag
      // Text after [cut] is regular content, not hidden
      const bbcode = "[cut]some text after";
      const html = bbcodeToHtml(bbcode);
      expect(html).toContain("<hr");
      expect(html).toContain("some text after");
      // Should NOT create a block - content is NOT wrapped
      expect(html).not.toContain("</div>");
    });

    it("multiple [cut] markers work", () => {
      const bbcode = "part1[cut]part2[cut]part3";
      const html = bbcodeToHtml(bbcode);
      expect(html.match(/<hr[^>]*>/g)?.length).toBe(2);
    });
  });
});

// ============================================================================
// MOD BLOCK - DM3 EXTENSION
// ============================================================================

describe("BBCode Mod Block (DM3 extension)", () => {
  describe("[mod]", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[mod]moderator note[/mod]");
      expect(html).toContain('data-bb-tag="mod"');
      expect(html).toContain('class="bb-mod"');
    });

    it("survives round-trip", () => {
      const bbcode = "[mod]for moderators only[/mod]";
      const html = bbcodeToHtml(bbcode);
      expect(htmlToBbcode(html)).toBe(bbcode);
    });
  });
});

// ============================================================================
// IMAGES
// ============================================================================

describe("BBCode Images", () => {
  describe("[img]URL[/img] DM2 format", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[img]https://example.com/image.png[/img]");
      expect(html).toContain('data-bb-tag="img"');
      expect(html).toContain('src="https://example.com/image.png"');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[img]https://example.com/photo.jpg[/img]");
    });

    it("adds empty alt for accessibility", () => {
      const html = bbcodeToHtml("[img]https://example.com/photo.jpg[/img]");
      expect(html).toContain('alt=""');
    });

    it("handles URL with special characters", () => {
      const html = bbcodeToHtml(
        "[img]https://example.com/photo.jpg?size=large&format=webp[/img]",
      );
      expect(html).toContain(
        'src="https://example.com/photo.jpg?size=large&amp;format=webp"',
      );
    });

    it("converts unmarked images to [img]URL[/img] format", () => {
      const result = htmlToBbcode(
        '<img src="https://example.com/photo.jpg" />',
      );
      expect(result).toBe("[img]https://example.com/photo.jpg[/img]");
    });

    it("handles unmarked images with alt (ignores alt)", () => {
      const result = htmlToBbcode(
        '<img src="https://example.com/cat.jpg" alt="A cute cat" />',
      );
      expect(result).toBe("[img]https://example.com/cat.jpg[/img]");
    });
  });
});

// ============================================================================
// PRIVATE TEXT
// ============================================================================

describe("BBCode Private", () => {
  describe("[private=character]", () => {
    it("converts to HTML with character marker", () => {
      const html = bbcodeToHtml(
        "[private=CharacterName]secret message[/private]",
      );
      expect(html).toContain('data-bb-tag="private"');
      expect(html).toContain('data-bb-character="CharacterName"');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[private=MyCharacter]for character only[/private]");
    });

    it("handles character names with spaces", () => {
      expectRoundTrip("[private=John Smith]message[/private]");
    });
  });
});

// ============================================================================
// NEWLINES AND WHITESPACE
// ============================================================================

describe("BBCode Newlines and Whitespace", () => {
  describe("line breaks", () => {
    it("converts newlines to separate paragraphs", () => {
      const html = bbcodeToHtml("line1\nline2");
      expect(html).toBe("<p>line1</p><p>line2</p>");
    });

    it("newlines survive round-trip", () => {
      expectRoundTrip("line1\nline2");
    });

    it("multiple newlines are preserved (up to 2)", () => {
      const result = htmlToBbcode(bbcodeToHtml("a\n\nb"));
      expect(result).toBe("a\n\nb");
    });

    it("excessive newlines are normalized to 2", () => {
      // 4 newlines = 3 blank lines, which get normalized to 2 blank lines = 2 newlines
      const result = htmlToBbcode(bbcodeToHtml("a\n\n\n\nb"));
      expect(result).toBe("a\n\nb");
    });
  });
});

// ============================================================================
// HTML ESCAPING
// ============================================================================

describe("BBCode HTML Escaping", () => {
  it("escapes < > & \" ' in content", () => {
    const html = bbcodeToHtml("1 < 2 && 3 > 2");
    expect(html).toContain("&lt;");
    expect(html).toContain("&gt;");
    expect(html).toContain("&amp;");
  });

  it("unescapes HTML entities back to characters", () => {
    const bbcode = "1 < 2 && 3 > 2";
    expectRoundTrip(bbcode);
  });

  it("preserves special characters in formatted text", () => {
    expectRoundTrip("[b]a < b && c > d[/b]");
  });
});

// ============================================================================
// NESTED TAGS
// ============================================================================

describe("BBCode Nested Tags", () => {
  it("handles simple nesting", () => {
    expectRoundTrip("[b][i]bold italic[/i][/b]");
  });

  it("handles deep nesting", () => {
    expectRoundTrip("[b][i][u]deeply nested[/u][/i][/b]");
  });

  it("handles mixed formatting", () => {
    expectRoundTrip("[b]bold[/b] normal [i]italic[/i]");
  });

  it("handles formatting in quotes", () => {
    expectRoundTrip("[quote][b]bold quote[/b][/quote]");
  });

  it("handles lists with formatting", () => {
    expectRoundTrip(
      "[ul][li][b]bold item[/b][/li][li][i]italic item[/i][/li][/ul]",
    );
  });
});

// ============================================================================
// COMPLEX COMBINATIONS
// ============================================================================

describe("BBCode Complex Combinations", () => {
  it("handles mixed content", () => {
    const bbcode =
      "[b]Bold[/b]\nNormal\n[i]Italic[/i]\n[code]code block[/code]";
    expectRoundTrip(bbcode);
  });

  it("handles spoiler with formatting inside", () => {
    expectRoundTrip(
      "[spoiler][b]hidden bold[/b] and [i]hidden italic[/i][/spoiler]",
    );
  });

  it("handles quote with link (DM3 format)", () => {
    expectRoundTrip(
      "[quote][link=link in quote]https://example.com[/link][/quote]",
    );
  });

  it("handles multiple tabs with content", () => {
    expectRoundTrip("Column1[tab]Column2[tab]Column3");
  });

  it("handles list with nested spoiler", () => {
    expectRoundTrip("[ul][li][spoiler]hidden item[/spoiler][/li][/ul]");
  });
});

// ============================================================================
// UNMARKED HTML FALLBACK
// ============================================================================

describe("HTML to BBCode - Unmarked Elements", () => {
  it("converts unmarked <strong> to [b]", () => {
    expect(htmlToBbcode("<strong>text</strong>")).toBe("[b]text[/b]");
  });

  it("converts unmarked <b> to [b]", () => {
    expect(htmlToBbcode("<b>text</b>")).toBe("[b]text[/b]");
  });

  it("converts unmarked <em> to [i]", () => {
    expect(htmlToBbcode("<em>text</em>")).toBe("[i]text[/i]");
  });

  it("converts unmarked <i> to [i]", () => {
    expect(htmlToBbcode("<i>text</i>")).toBe("[i]text[/i]");
  });

  it("converts unmarked <s>, <del>, <strike> to [strike]", () => {
    expect(htmlToBbcode("<s>text</s>")).toBe("[strike]text[/strike]");
    expect(htmlToBbcode("<del>text</del>")).toBe("[strike]text[/strike]");
    expect(htmlToBbcode("<strike>text</strike>")).toBe("[strike]text[/strike]");
  });

  it("converts unmarked links correctly (DM3 format)", () => {
    // DM3: [link=text]URL[/link]
    expect(htmlToBbcode('<a href="https://example.com">text</a>')).toBe(
      "[link=text]https://example.com[/link]",
    );
  });

  it("converts self-referential unmarked links", () => {
    expect(
      htmlToBbcode('<a href="https://example.com">https://example.com</a>'),
    ).toBe("[link]https://example.com[/link]");
  });

  it('converts links displaying "ссылка" to simple form', () => {
    expect(htmlToBbcode('<a href="https://example.com">ссылка</a>')).toBe(
      "[link]https://example.com[/link]",
    );
  });

  it("converts <br> to newline", () => {
    expect(htmlToBbcode("line1<br>line2")).toBe("line1\nline2");
    expect(htmlToBbcode("line1<br />line2")).toBe("line1\nline2");
    expect(htmlToBbcode("line1<br/>line2")).toBe("line1\nline2");
  });
});

// ============================================================================
// VALIDATION
// ============================================================================

describe("validateBBCode", () => {
  it("returns empty array for valid BBCode", () => {
    expect(validateBBCode("[b]text[/b]")).toEqual([]);
  });

  it("returns empty array for nested valid BBCode", () => {
    expect(validateBBCode("[b][i]text[/i][/b]")).toEqual([]);
  });

  it("returns empty array for standalone tags", () => {
    expect(validateBBCode("[tab]")).toEqual([]);
  });

  it("detects unclosed tags", () => {
    const errors = validateBBCode("[b]text");
    expect(errors).toContain("Unclosed tag: [b]");
  });

  it("detects mismatched tags", () => {
    const errors = validateBBCode("[b][i]text[/b][/i]");
    expect(errors.some((e) => e.includes("Mismatched"))).toBe(true);
  });

  it("detects unexpected closing tags", () => {
    const errors = validateBBCode("text[/b]");
    expect(errors.some((e) => e.includes("Unexpected"))).toBe(true);
  });

  it("handles complex valid nested structures", () => {
    expect(validateBBCode("[quote][b][i]text[/i][/b][/quote]")).toEqual([]);
  });
});

// ============================================================================
// PLAIN TEXT EXTRACTION
// ============================================================================

describe("bbcodeToPlainText", () => {
  it("removes all formatting tags", () => {
    expect(bbcodeToPlainText("[b]bold[/b] [i]italic[/i]")).toBe("bold italic");
  });

  it("removes attribute tags", () => {
    expect(bbcodeToPlainText("[link=http://example.com]text[/link]")).toBe(
      "text",
    );
  });

  it("removes standalone tags", () => {
    expect(bbcodeToPlainText("before[tab]after")).toBe("before after");
  });

  it("normalizes whitespace", () => {
    expect(bbcodeToPlainText("[b]text[/b]   [i]more[/i]")).toBe("text more");
  });

  it("handles empty input", () => {
    expect(bbcodeToPlainText("")).toBe("");
  });

  it("handles complex nested BBCode", () => {
    expect(bbcodeToPlainText("[quote][b]quoted[/b][/quote]")).toBe("quoted");
  });
});

// ============================================================================
// CONTEXT TAGS
// ============================================================================

describe("Context Tag Availability", () => {
  describe("isTagAvailable", () => {
    it("returns true for common tags", () => {
      expect(isTagAvailable("b", "common")).toBe(true);
      expect(isTagAvailable("i", "common")).toBe(true);
      expect(isTagAvailable("strike", "common")).toBe(true);
    });

    it("[private] only available in post", () => {
      expect(isTagAvailable("private", "post")).toBe(true);
      expect(isTagAvailable("private", "message")).toBe(false);
      expect(isTagAvailable("private", "common")).toBe(false);
    });

    it("[mod] available in common and message only", () => {
      expect(isTagAvailable("mod", "common")).toBe(true);
      expect(isTagAvailable("mod", "message")).toBe(true);
      expect(isTagAvailable("mod", "post")).toBe(false);
      expect(isTagAvailable("mod", "info")).toBe(false);
    });

    it("[nsfw] available in all contexts", () => {
      expect(isTagAvailable("nsfw", "common")).toBe(true);
      expect(isTagAvailable("nsfw", "message")).toBe(true);
      expect(isTagAvailable("nsfw", "post")).toBe(true);
    });

    it("[noparse] available in all contexts", () => {
      expect(isTagAvailable("noparse", "common")).toBe(true);
      expect(isTagAvailable("noparse", "message")).toBe(true);
      expect(isTagAvailable("noparse", "post")).toBe(true);
    });

    it("is case insensitive", () => {
      expect(isTagAvailable("B", "common")).toBe(true);
      expect(isTagAvailable("BOLD", "common")).toBe(false);
    });
  });

  describe("stripUnavailableTags", () => {
    it("removes [private] from message context", () => {
      const result = stripUnavailableTags(
        "[b]bold[/b] [private=char]secret[/private]",
        "message",
      );
      expect(result).toBe("[b]bold[/b] secret");
    });

    it("keeps [private] in post context", () => {
      const result = stripUnavailableTags(
        "[private=char]secret[/private]",
        "post",
      );
      expect(result).toBe("[private=char]secret[/private]");
    });

    it("removes [mod] from post context", () => {
      const result = stripUnavailableTags("[mod]mod note[/mod]", "post");
      expect(result).toBe("mod note");
    });

    it("keeps [mod] in message context", () => {
      const result = stripUnavailableTags("[mod]mod note[/mod]", "message");
      expect(result).toBe("[mod]mod note[/mod]");
    });
  });
});

// ============================================================================
// CONTEXT TAGS STRUCTURE
// ============================================================================

describe("CONTEXT_TAGS structure", () => {
  const contexts: BBCodeContext[] = ["common", "post", "info", "message"];

  it("all contexts have basic formatting tags", () => {
    const basicTags = ["b", "i", "u", "strike", "code"];
    contexts.forEach((ctx) => {
      basicTags.forEach((tag) => {
        expect(CONTEXT_TAGS[ctx]).toContain(tag);
      });
    });
  });

  it("post has private but NOT mod", () => {
    expect(CONTEXT_TAGS.post).toContain("private");
    expect(CONTEXT_TAGS.post).not.toContain("mod");
  });

  it("common and message have mod but NOT private", () => {
    expect(CONTEXT_TAGS.common).toContain("mod");
    expect(CONTEXT_TAGS.message).toContain("mod");
    expect(CONTEXT_TAGS.common).not.toContain("private");
    expect(CONTEXT_TAGS.message).not.toContain("private");
  });

  it("all contexts have nsfw and noparse", () => {
    contexts.forEach((ctx) => {
      expect(CONTEXT_TAGS[ctx]).toContain("nsfw");
      expect(CONTEXT_TAGS[ctx]).toContain("noparse");
    });
  });

  it("all contexts have cut (DM3 extension)", () => {
    expect(CONTEXT_TAGS.common).toContain("cut");
    expect(CONTEXT_TAGS.post).toContain("cut");
    expect(CONTEXT_TAGS.message).toContain("cut");
    expect(CONTEXT_TAGS.info).toContain("cut");
  });

  it("info does not have mod or private", () => {
    expect(CONTEXT_TAGS.info).not.toContain("mod");
    expect(CONTEXT_TAGS.info).not.toContain("private");
  });
});

// ============================================================================
// EDGE CASES
// ============================================================================

describe("Edge Cases", () => {
  it("handles empty string", () => {
    expect(bbcodeToHtml("")).toBe("");
    expect(htmlToBbcode("")).toBe("");
  });

  it("handles plain text without tags", () => {
    // Plain text is wrapped in paragraph for Tiptap structure
    expect(bbcodeToHtml("plain text")).toBe("<p>plain text</p>");
    expect(htmlToBbcode("<p>plain text</p>")).toBe("plain text");
  });

  it("handles unclosed tags gracefully", () => {
    // Should not crash, content preserved
    const html = bbcodeToHtml("[b]unclosed");
    expect(html).toContain("[b]unclosed");
  });

  it("handles empty tags", () => {
    expectRoundTrip("[b][/b]");
    expectRoundTrip("[i][/i]");
  });

  it("handles adjacent tags", () => {
    expectRoundTrip("[b]bold[/b][i]italic[/i]");
  });

  it("handles tags with numbers in attribute", () => {
    expectRoundTrip("[private=Character123]text[/private]");
  });

  it("handles URLs with special characters (DM3 format)", () => {
    expectRoundTrip("[link=link]https://example.com/path?a=1&b=2[/link]");
  });

  it("handles case insensitive tags", () => {
    const html = bbcodeToHtml("[B]bold[/B]");
    expect(html).toContain("<strong");
    expect(htmlToBbcode(html)).toBe("[b]bold[/b]");
  });
});

// ============================================================================
// REGRESSION TESTS
// ============================================================================

describe("Regression Tests", () => {
  it("tabs should not be lost in conversion", () => {
    const original = "Hello[tab]World";
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe(original);
    expect(result).toContain("[tab]");
  });

  it("[strike] should stay as [strike] after round-trip (DM2 format)", () => {
    const original = "[strike]deleted[/strike]";
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe("[strike]deleted[/strike]");
  });

  it("[link] preserves format after round-trip", () => {
    const original = "[link=text]https://example.com[/link]";
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe(original);
    expect(result).toContain("[link");
  });

  // Escaping round-trip tests (prevent entity accumulation)
  it("[link=Tom's Page] preserves apostrophe after round-trip", () => {
    const original = "[link=Tom's Page]https://example.com[/link]";
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe(original);
    expect(result).not.toContain("&#039;");
  });

  it("[private=O'Brien] preserves apostrophe after round-trip", () => {
    const original = "[private=O'Brien]secret[/private]";
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe(original);
    expect(result).not.toContain("&#039;");
  });

  it("multiple round-trips do not accumulate entities", () => {
    const original = "[link=Tom's Page]https://example.com[/link]";
    let current = original;
    // Perform 5 round-trips
    for (let i = 0; i < 5; i++) {
      const html = bbcodeToHtml(current);
      current = htmlToBbcode(html);
    }
    expect(current).toBe(original);
  });

  it("code block newlines should be preserved", () => {
    const original = "[code]line1\nline2\nline3[/code]";
    expectRoundTrip(original);
  });

  it("standalone [cut] should stay standalone", () => {
    const original = "[cut]";
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe("[cut]");
  });

  it("[cut] is standalone only - no closing tag needed", () => {
    // [cut] does NOT have a closing tag
    // If someone types [cut]text[/cut], only [cut] is processed
    const input = "before[cut]after";
    const html = bbcodeToHtml(input);
    const result = htmlToBbcode(html);
    expect(result).toBe("before[cut]after");
  });
});

// ============================================================================
// XSS PREVENTION - URL SANITIZATION
// ============================================================================

describe("XSS Prevention - sanitizeUrl", () => {
  describe("dangerous protocols", () => {
    it("blocks javascript: protocol", () => {
      expect(sanitizeUrl("javascript:alert(1)")).toBe("#");
    });

    it("blocks javascript: with mixed case", () => {
      expect(sanitizeUrl("JaVaScRiPt:alert(1)")).toBe("#");
    });

    it("blocks javascript: with leading spaces", () => {
      expect(sanitizeUrl("  javascript:alert(1)")).toBe("#");
    });

    it("blocks data: protocol", () => {
      expect(sanitizeUrl("data:text/html,<script>alert(1)</script>")).toBe("#");
    });

    it("blocks data: base64 encoded", () => {
      expect(
        sanitizeUrl(
          "data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==",
        ),
      ).toBe("#");
    });

    it("blocks vbscript: protocol", () => {
      expect(sanitizeUrl('vbscript:msgbox("XSS")')).toBe("#");
    });
  });

  describe("safe protocols", () => {
    it("allows http: links", () => {
      expect(sanitizeUrl("http://example.com")).toBe("http://example.com");
    });

    it("allows https: links", () => {
      expect(sanitizeUrl("https://example.com")).toBe("https://example.com");
    });

    it("allows mailto: links", () => {
      expect(sanitizeUrl("mailto:user@example.com")).toBe(
        "mailto:user@example.com",
      );
    });

    it("allows tel: links", () => {
      expect(sanitizeUrl("tel:+1234567890")).toBe("tel:+1234567890");
    });

    it("allows protocol-relative URLs", () => {
      expect(sanitizeUrl("//example.com/path")).toBe("//example.com/path");
    });

    it("allows relative URLs", () => {
      expect(sanitizeUrl("/path/to/page")).toBe("/path/to/page");
    });

    it("allows relative URLs without leading slash", () => {
      expect(sanitizeUrl("path/to/page")).toBe("path/to/page");
    });
  });

  describe("unsafe protocols blocked", () => {
    it("blocks ftp: protocol", () => {
      expect(sanitizeUrl("ftp://files.example.com")).toBe("#");
    });

    it("blocks file: protocol", () => {
      expect(sanitizeUrl("file:///etc/passwd")).toBe("#");
    });

    it("blocks custom protocols", () => {
      expect(sanitizeUrl("custom://something")).toBe("#");
    });
  });

  describe("edge cases", () => {
    it("handles empty string", () => {
      expect(sanitizeUrl("")).toBe("#");
    });

    it("handles URL with query params", () => {
      expect(sanitizeUrl("https://example.com?a=1&b=2")).toBe(
        "https://example.com?a=1&b=2",
      );
    });

    it("handles URL with hash", () => {
      expect(sanitizeUrl("https://example.com#section")).toBe(
        "https://example.com#section",
      );
    });

    it("trims whitespace", () => {
      expect(sanitizeUrl("  https://example.com  ")).toBe(
        "https://example.com",
      );
    });
  });
});

describe("XSS Prevention - sanitizeImageUrl", () => {
  describe("allowed protocols", () => {
    it("allows https: images", () => {
      expect(sanitizeImageUrl("https://example.com/image.png")).toBe(
        "https://example.com/image.png",
      );
    });

    it("allows http: images", () => {
      expect(sanitizeImageUrl("http://example.com/image.jpg")).toBe(
        "http://example.com/image.jpg",
      );
    });
  });

  describe("blocked protocols", () => {
    it("blocks javascript: images", () => {
      expect(sanitizeImageUrl("javascript:alert(1)")).toBe("#");
    });

    it("blocks data: images", () => {
      expect(
        sanitizeImageUrl('data:image/svg+xml,<svg onload="alert(1)"/>'),
      ).toBe("#");
    });

    it("blocks mailto: for images", () => {
      expect(sanitizeImageUrl("mailto:user@example.com")).toBe("#");
    });

    it("blocks tel: for images", () => {
      expect(sanitizeImageUrl("tel:+1234567890")).toBe("#");
    });
  });
});

describe("XSS Prevention - bbcodeToHtml", () => {
  describe("[link] XSS prevention (DM3 format)", () => {
    it("sanitizes javascript: in link URL (content)", () => {
      // DM3 format: [link=text]URL[/link] - URL is the content
      const html = bbcodeToHtml("[link=click me]javascript:alert(1)[/link]");
      expect(html).toContain('href="#"');
      expect(html).not.toContain("javascript:");
    });

    it("sanitizes javascript: in self-ref link", () => {
      const html = bbcodeToHtml("[link]javascript:alert(1)[/link]");
      expect(html).toContain('href="#"');
    });

    it("preserves safe links", () => {
      const html = bbcodeToHtml("[link=safe]https://example.com[/link]");
      expect(html).toContain('href="https://example.com"');
    });
  });

  describe("[img]URL[/img] XSS prevention (DM2 format)", () => {
    it("sanitizes javascript: in img src", () => {
      const html = bbcodeToHtml("[img]javascript:alert(1)[/img]");
      expect(html).toContain('src="#"');
      expect(html).not.toContain("javascript:");
    });

    it("sanitizes data: in img src", () => {
      const html = bbcodeToHtml(
        '[img]data:image/svg+xml,<svg onload="alert(1)"/>[/img]',
      );
      expect(html).toContain('src="#"');
      expect(html).not.toContain("data:");
    });

    it("preserves safe image URLs", () => {
      const html = bbcodeToHtml("[img]https://example.com/photo.jpg[/img]");
      expect(html).toContain('src="https://example.com/photo.jpg"');
    });
  });
});

describe("XSS Prevention - htmlToBbcode", () => {
  describe("unmarked link XSS prevention", () => {
    it("sanitizes javascript: links from external HTML", () => {
      const result = htmlToBbcode('<a href="javascript:alert(1)">click</a>');
      expect(result).toBe("click");
      expect(result).not.toContain("[link");
    });

    it("sanitizes data: links from external HTML", () => {
      const result = htmlToBbcode(
        '<a href="data:text/html,<script>alert(1)</script>">xss</a>',
      );
      expect(result).toBe("xss");
      expect(result).not.toContain("[link");
    });

    it("preserves safe links from external HTML (DM3 format)", () => {
      const result = htmlToBbcode('<a href="https://safe.com">safe link</a>');
      expect(result).toBe("[link=safe link]https://safe.com[/link]");
    });
  });

  describe("unmarked image XSS prevention", () => {
    it("removes javascript: images from external HTML", () => {
      const result = htmlToBbcode('<img src="javascript:alert(1)" />');
      expect(result).toBe("");
      expect(result).not.toContain("[img");
    });

    it("removes data: images from external HTML", () => {
      const result = htmlToBbcode(
        '<img src="data:image/svg+xml,<svg onload=alert(1)/>" />',
      );
      expect(result).toBe("");
      expect(result).not.toContain("[img");
    });

    it("preserves safe images from external HTML", () => {
      const result = htmlToBbcode(
        '<img src="https://example.com/image.png" />',
      );
      expect(result).toBe("[img]https://example.com/image.png[/img]");
    });
  });

  describe("marked link XSS prevention", () => {
    it("sanitizes javascript: in marked links", () => {
      const result = htmlToBbcode(
        '<a href="javascript:alert(1)" data-bb-tag="link">text</a>',
      );
      expect(result).toBe("text");
      expect(result).not.toContain("[link");
    });

    it("sanitizes data: in marked links", () => {
      const result = htmlToBbcode(
        '<a href="data:text/html,evil" data-bb-tag="link">text</a>',
      );
      expect(result).toBe("text");
      expect(result).not.toContain("[link");
    });
  });

  describe("marked image XSS prevention", () => {
    it("removes javascript: from marked images", () => {
      const result = htmlToBbcode(
        '<img src="javascript:alert(1)" data-bb-tag="img" />',
      );
      expect(result).toBe("");
    });

    it("removes data: from marked images", () => {
      const result = htmlToBbcode(
        '<img src="data:image/png;base64,evil" data-bb-tag="img" />',
      );
      expect(result).toBe("");
    });
  });
});

describe("XSS Prevention - Round Trip Safety", () => {
  it("dangerous links do not survive round-trip", () => {
    // DM3 format: [link=text]URL[/link]
    const malicious = "[link=click]javascript:alert(1)[/link]";
    const html = bbcodeToHtml(malicious);
    const result = htmlToBbcode(html);
    expect(result).not.toContain("javascript:");
    expect(result).toBe("click"); // Link stripped, text preserved
  });

  it("dangerous images do not survive round-trip", () => {
    const malicious = "[img]javascript:alert(1)[/img]";
    const html = bbcodeToHtml(malicious);
    // Dangerous image should have # as src, and should be stripped on conversion back
    expect(html).toContain('src="#"');
  });

  it("safe URLs survive round-trip", () => {
    const safe = "[link=safe link]https://example.com[/link]";
    const html = bbcodeToHtml(safe);
    const result = htmlToBbcode(html);
    expect(result).toBe(safe);
  });

  it("safe images survive round-trip", () => {
    const safe = "[img]https://example.com/photo.jpg[/img]";
    const html = bbcodeToHtml(safe);
    const result = htmlToBbcode(html);
    expect(result).toBe(safe);
  });
});

// ============================================================================
// PASTE HTML CLEANUP
// ============================================================================

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

    it("Google Docs HTML converts cleanly (DM3 format)", () => {
      const gdocsHtml = `
        <span style="font-size:11pt;font-family:Arial;">
          <strong>Bold</strong> text with <a href="https://example.com">a link</a>
        </span>
      `;
      const cleaned = cleanPastedHtml(gdocsHtml);
      const bbcode = htmlToBbcode(cleaned);
      expect(bbcode).toContain("[b]Bold[/b]");
      // DM3 format: [link=text]URL[/link]
      expect(bbcode).toContain("[link=a link]https://example.com[/link]");
      expect(bbcode).not.toContain("font-size");
    });
  });
});
