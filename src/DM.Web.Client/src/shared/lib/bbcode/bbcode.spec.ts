/**
 * The converter of the editor: formatting, [code] and [noparse], [tab] and
 * [cut], line breaks, escaping, nesting, foreign HTML, the tag sets and the
 * edges. A finding of validateBBCode lives in bbcodeWarnings.spec.ts; anything
 * else lives in the spec of its tag or rule (bbcodeBlocks, bbcodePrivate,
 * bbcodeAddresses, bbcodeSpelling, bbcodeCost, bbcodePaste), or here.
 */

import { describe, it, expect } from "vitest";
import { bbcodeToHtml } from "./bbcodeToHtml";
import { htmlToBbcode } from "./htmlToBbcode";
import { validateBBCode } from "./bbcodeWarnings";
import { CONTEXT_TAGS, type BBCodeContext } from "./bbcodeTagSet";
import {
  expectRoundTrip,
  expectToHtml,
} from "@/shared/lib/testing/converterRoundTrip";

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

    // [code] changes the font and nothing else, as on DM2: the markup inside it
    // works.
    it("draws the markup inside it, as [b] does", () => {
      const bbcode = "[code][b]bold[/b] [i]italic[/i][/code]";
      expect(bbcodeToHtml(bbcode)).toBe(
        '<p><code data-bb-tag="code"><strong data-bb-tag="b">bold</strong> <em data-bb-tag="i">italic</em></code></p>',
      );
      expectRoundTrip(bbcode);
    });

    it("stands in the line, and holds a block when one is written in it", () => {
      expect(bbcodeToHtml("раз [code]два[/code] три")).toBe(
        '<p>раз <code data-bb-tag="code">два</code> три</p>',
      );
      const bbcode = "раз [code][quote]два[/quote][/code] три";
      expect(bbcodeToHtml(bbcode)).toContain(
        '<code data-bb-tag="code" data-bb-glue-before="true" data-bb-glue-after="true"><blockquote class="bb-quote" data-bb-tag="quote"><p>два</p></blockquote></code>',
      );
      expectRoundTrip(bbcode);
    });

    // The DM2 way to show tags as typed in a monospace font.
    it("holds a [noparse] example", () => {
      const bbcode = "[code][noparse][b]пример[/b][/noparse][/code]";
      expect(bbcodeToHtml(bbcode)).toBe(
        '<p><code data-bb-tag="code"><span class="bb-noparse" data-bb-tag="noparse">[b]пример[/b]</span></code></p>',
      );
      expectRoundTrip(bbcode);
    });

    // Left open, it is a tag left open like any other: the [private] after it
    // is a hidden block, and the save takes it.
    it("left open, leaves a hidden block after it alone", () => {
      const bbcode = '[code]пример [private="Вася"]секрет[/private]';
      expect(bbcodeToHtml(bbcode, { context: "post" })).toContain(
        'data-bb-tag="private"',
      );
      expect(
        validateBBCode(bbcode, "post").map((issue) => issue.reason),
      ).toEqual(["unclosed"]);
    });

    it("reads back from the markup of the server and of other pages", () => {
      expect(
        htmlToBbcode('<code class="code"><strong>x</strong> y</code>'),
      ).toBe("[code][b]x[/b] y[/code]");
      expect(htmlToBbcode("<pre>a  b</pre>")).toBe("[code]a  b[/code]");
    });
  });
});

// The other special blocks stand in bbcodeBlocks.spec.ts.
describe("BBCode Special Blocks", () => {
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
});

describe("BBCode Tab and Cut", () => {
  describe("[tab]", () => {
    it("converts to non-breaking spaces with marker", () => {
      const html = bbcodeToHtml("[tab]");
      expect(html).toContain('data-bb-tag="tab"');
      // Three of them, the width the server draws it with.
      expect(html).toContain(">&nbsp;&nbsp;&nbsp;</span>");
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

  // [cut] is no tag, on the server or here: it is text, as typed.
  describe("[cut]", () => {
    it("stays in the text as typed", () => {
      expectToHtml("before[cut]after", "<p>before[cut]after</p>");
      expectRoundTrip("part1[cut]part2[cut]part3");
      expectRoundTrip("[cut]раз[/cut]");
    });
  });
});

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

    // Every line the author typed comes back, blank ones included: the
    // editor has no business shortening a text.
    it("keeps any number of blank lines", () => {
      expectRoundTrip("a\n\n\n\nb");
    });

    it("keeps the spaces a line starts with", () => {
      expectRoundTrip("раз\n ___\n  два");
    });
  });
});

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
      '[quote][link text="link in quote"]https://example.com[/link][/quote]',
    );
  });

  it("handles multiple tabs with content", () => {
    expectRoundTrip("Column1[tab]Column2[tab]Column3");
  });

  it("handles list with nested spoiler", () => {
    expectRoundTrip("[ul][li][spoiler]hidden item[/spoiler][/li][/ul]");
  });
});

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

  it("converts unmarked links with their own text", () => {
    expect(htmlToBbcode('<a href="https://example.com">text</a>')).toBe(
      '[link text="text"]https://example.com[/link]',
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

describe("CONTEXT_TAGS structure", () => {
  const contexts: BBCodeContext[] = [
    "common",
    "post",
    "info",
    "message",
    "direct",
  ];

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

  it("no context exposes [cut], which is no tag", () => {
    contexts.forEach((ctx) => {
      expect(CONTEXT_TAGS[ctx]).not.toContain("cut");
    });
  });

  // Private messages do not render [mod]; the global chat does.
  it("direct has neither mod nor private", () => {
    expect(CONTEXT_TAGS.direct).not.toContain("mod");
    expect(CONTEXT_TAGS.direct).not.toContain("private");
    expect(bbcodeToHtml("[mod]текст[/mod]", { context: "direct" })).toBe(
      "<p>[mod]текст[/mod]</p>",
    );
    expect(bbcodeToHtml("[mod]текст[/mod]", { context: "message" })).toContain(
      'data-bb-tag="mod"',
    );
  });

  it("draws a tag of no other context as text", () => {
    expect(
      bbcodeToHtml('[private="Вася"]секрет[/private]', { context: "common" }),
    ).toBe('<p>[private="Вася"]секрет[/private]</p>');
  });

  it("info does not have mod or private", () => {
    expect(CONTEXT_TAGS.info).not.toContain("mod");
    expect(CONTEXT_TAGS.info).not.toContain("private");
  });
});

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
    expectRoundTrip('[private="Character123"]text[/private]');
  });

  it("handles URLs with special characters", () => {
    expectRoundTrip(
      '[link text="link"]https://example.com/path?a=1&b=2[/link]',
    );
  });
});

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
    const original = '[link text="text"]https://example.com[/link]';
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe(original);
    expect(result).toContain("[link");
  });

  // Escaping round-trip tests (prevent entity accumulation)
  it('[link text="Tom\'s Page"] preserves apostrophe after round-trip', () => {
    const original = '[link text="Tom\'s Page"]https://example.com[/link]';
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe(original);
    expect(result).not.toContain("&#039;");
  });

  it('[private="O\'Brien"] preserves apostrophe after round-trip', () => {
    const original = '[private="O\'Brien"]secret[/private]';
    const html = bbcodeToHtml(original);
    const result = htmlToBbcode(html);
    expect(result).toBe(original);
    expect(result).not.toContain("&#039;");
  });

  it("multiple round-trips do not accumulate entities", () => {
    const original = '[link text="Tom\'s Page"]https://example.com[/link]';
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

  it("[cut] stays in the text through a round trip", () => {
    expectRoundTrip("before[cut]after");
  });
});
