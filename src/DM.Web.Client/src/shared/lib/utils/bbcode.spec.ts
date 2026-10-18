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

    // [code] changes the font and nothing else, as on DM2: the markup inside
    // it works. It used to be an example, which showed the tags as typed.
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

// ============================================================================
// LINKS
// ============================================================================

describe("BBCode Links", () => {
  describe('[link text="..."]URL[/link] - its own text', () => {
    it("converts to HTML with custom text displayed", () => {
      const bbcode = '[link text="Click here"]https://example.com[/link]';
      const html = bbcodeToHtml(bbcode);
      expect(html).toContain('data-bb-tag="link"');
      expect(html).toContain('data-bb-text="Click here"');
      expect(html).toContain(">Click here<");
      expect(html).toContain('href="https://example.com"');
    });

    it("survives round-trip", () => {
      expectRoundTrip('[link text="Click here"]https://example.com[/link]');
    });

    // [link=text] was a spelling of DM3's own, never of DM2: it is a broken
    // tag now, shown as typed, and the editor gives it back as typed.
    it("leaves the old [link=text] as the text it is", () => {
      const bbcode = "[link=Click here]https://example.com[/link]";
      expect(bbcodeToHtml(bbcode)).toBe(`<p>${bbcode}</p>`);
      expectRoundTrip(bbcode);
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
      const html = bbcodeToHtml('[link text="text"]https://example.com[/link]');
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

  // Text written inside a list but outside its items.
  //
  // The display path used to drop it and this converter never did, so a
  // paragraph between two items sat in the editor, in the stored source and in
  // search, and reached no reader. The server prints it now, lifting it out of
  // the list markup because a ul may hold nothing but li (TagNode.EmitList).
  //
  // This converter keeps the shape it had, and the difference is deliberate:
  // it builds the HTML the editor edits, not the HTML a reader gets, and its
  // contract is that the source survives a round trip untouched. Lifting the
  // text here would move it out of the list in the author's own source the
  // first time they opened the post in visual mode. What the two sides owe is
  // the same thing, and it is what these pin: the text is never lost.
  // A list that holds anything but items and the space between them is
  // drawn as a block of what it holds: the visual mode holds items and
  // nothing else in a list.
  describe("a list that holds more than items", () => {
    it.each([
      [
        "[ul]раз[/ul]",
        '<div data-bb-tag="ul" data-bb-loose="true"><p>раз</p></div>',
      ],
      ["[ol][/ol]", '<div data-bb-tag="ol" data-bb-loose="true"></div>'],
    ])("is a block of what it holds: %s", (bbcode, html) => {
      expect(bbcodeToHtml(bbcode)).toBe(html);
      expectRoundTrip(bbcode);
    });

    it("is a list when it holds items and the space between them", () => {
      expect(bbcodeToHtml("[ul] [li]раз[/li]\n[/ul]")).toBe(
        '<ul data-bb-tag="ul"> <li data-bb-tag="li"><p>раз</p></li>\n</ul>',
      );
    });

    it("reads back from the markup of the visual mode", () => {
      expect(
        htmlToBbcode(
          '<div data-bb-tag="ol" data-bb-loose="true"><p>раз</p><div data-bb-tag="li"><p>два</p></div></div>',
        ),
      ).toBe("[ol]раз\n[li]два[/li][/ol]");
    });
  });

  describe("text between items", () => {
    it("keeps text written between two items", () => {
      const bbcode = "[ul][li]item 1[/li]a word between[li]item 2[/li][/ul]";
      expect(bbcodeToHtml(bbcode)).toContain("a word between");
      expectRoundTrip(bbcode);
    });

    it("keeps text written before the first item", () => {
      const bbcode = "[ul]a word before[li]item 1[/li][li]item 2[/li][/ul]";
      expect(bbcodeToHtml(bbcode)).toContain("a word before");
      expectRoundTrip(bbcode);
    });

    it("keeps text written after the last item", () => {
      const bbcode = "[ul][li]item 1[/li][li]item 2[/li]a word after[/ul]";
      expect(bbcodeToHtml(bbcode)).toContain("a word after");
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

  describe('[spoiler text="..."]', () => {
    it("carries its caption to the button of the visual mode", () => {
      const html = bbcodeToHtml('[spoiler text="Карта"]secret[/spoiler]');
      expect(html).toContain('data-bb-text="Карта"');
      expectRoundTrip('[spoiler text="Карта"]secret[/spoiler]');
    });
  });

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

    // A quote sets text apart, as in DM2: the block holds the text and there
    // is no author line above it.
    it("converts to a block with nothing but the text in it", () => {
      expectToHtml(
        "[quote]text[/quote]",
        '<blockquote class="bb-quote" data-bb-tag="quote"><p>text</p></blockquote>',
      );
    });

    // The tag declares no attribute, so a value on it is read the way a value
    // on [spoiler] or [b] is (see "a value on a tag without an attribute"):
    // the opening tag stays text, and the editor hands back what was written.
    it("[quote=X] gives no author line", () => {
      expectToHtml("[quote=Имя]text[/quote]", "<p>[quote=Имя]text[/quote]</p>");
      expectRoundTrip("[quote=Имя]text[/quote]");
    });

    it('[quote="X"] gives no author line either', () => {
      expectToHtml(
        '[quote="Имя"]text[/quote]',
        '<p>[quote="Имя"]text[/quote]</p>',
      );
      expectRoundTrip('[quote="Имя"]text[/quote]');
    });

    it("reads a value on [quote] the way it reads one on [spoiler]", () => {
      const asQuote = bbcodeToHtml("[quote=Имя]text[/quote]");
      const asSpoiler = bbcodeToHtml("[spoiler=Имя]text[/spoiler]");
      expect(asQuote).toBe(asSpoiler.replace(/spoiler/g, "quote"));
    });
  });

  // The page draws no empty line for the one line break typed straight after a
  // block (BBCODE_RENDERING.md, "Перевод строки после блока"), and the preview
  // draws none either: that break is the edge of the paragraph under the
  // block, as the break over a block is the edge of the paragraph above it.
  describe("a line break after a block", () => {
    it("puts the text on the line under a quote", () => {
      const bbcode = "перед\n[quote]цитата[/quote]\nпосле";
      expectToHtml(
        bbcode,
        '<p>перед</p><blockquote class="bb-quote" data-bb-tag="quote"><p>цитата</p></blockquote><p>после</p>',
      );
      expectRoundTrip(bbcode);
    });

    it("puts the text on the line under a hidden block", () => {
      const bbcode = 'перед\n[private="Вася"]секрет[/private]\nпосле';
      expect(bbcodeToHtml(bbcode, { context: "post" })).toBe(
        '<p>перед</p><p><span class="bb-private" data-bb-tag="private" data-bb-character="Вася">секрет</span></p><p>после</p>',
      );
      expectRoundTrip(bbcode);
    });

    it("keeps a second line break as the empty line it is", () => {
      const bbcode = "[quote]цитата[/quote]\n\nпосле";
      expectToHtml(
        bbcode,
        '<blockquote class="bb-quote" data-bb-tag="quote"><p>цитата</p></blockquote><p></p><p>после</p>',
      );
      expectRoundTrip(bbcode);
    });
  });
});

// ============================================================================
// A VALUE ON A TAG WITHOUT AN ATTRIBUTE
// ============================================================================

/**
 * A tag that declares no parameter takes no value: [code="x"], [b=1] and
 * [quote="Имя"] are broken tags, shown as typed with their closing tag, and
 * what they hold is drawn as usual. That holds for [code] and [noparse] too: a
 * broken one is no statement "show this as typed", so a tag inside it is a tag.
 * The editor hands every such spelling back as it was typed.
 */
describe("a value on a tag without an attribute", () => {
  const PAIRED = [
    "b",
    "i",
    "u",
    "strike",
    "nsfw",
    "mod",
    "quote",
    "ul",
    "ol",
    "li",
    "code",
    "noparse",
  ];

  it.each(PAIRED)("in quotes leaves [%s] as text", (tag) => {
    const written = `[${tag}="Вася"]текст[/${tag}]`;
    expect(bbcodeToHtml(written)).toBe(`<p>${written}</p>`);
    expectRoundTrip(written);
  });

  it.each(PAIRED)("without quotes leaves [%s] as text", (tag) => {
    const written = `[${tag}=Вася]текст[/${tag}]`;
    expect(bbcodeToHtml(written)).toBe(`<p>${written}</p>`);
    expectRoundTrip(written);
  });

  it("leaves [spoiler] with a value as text: its caption is text=", () => {
    const written = '[spoiler="Подпись"]текст[/spoiler]';
    expect(bbcodeToHtml(written)).toBe(`<p>${written}</p>`);
    expectRoundTrip(written);
  });

  it("reads [tab], which has no closing tag, the same way", () => {
    expect(bbcodeToHtml('раз[tab="Вася"]два')).toBe(
      '<p>раз[tab="Вася"]два</p>',
    );
    expect(bbcodeToHtml("раз[tab=Вася]два")).toBe("<p>раз[tab=Вася]два</p>");
  });

  it("draws what a broken [code] holds, and a broken [noparse] too", () => {
    const html = bbcodeToHtml('[code="x"][b]текст[/b][/code]');
    expect(html).toBe(
      '<p>[code="x"]<strong data-bb-tag="b">текст</strong>[/code]</p>',
    );
    expect(bbcodeToHtml("[noparse=x][b]текст[/b][/noparse]")).toContain(
      '<strong data-bb-tag="b">',
    );
    expectRoundTrip('[code="x"][b]текст[/b][/code]');
  });

  it("leaves a value written inside [code] and [noparse] as written", () => {
    expectRoundTrip('[noparse][b="Вася"]текст[/b][/noparse]');
    expectRoundTrip('[code][quote="Вася"]текст[/quote][/code]');
  });

  it("does not close a tag with a closing tag that carries a value", () => {
    expect(bbcodeToHtml('[b]текст[/b="x"][/b]')).toBe(
      '<p><strong data-bb-tag="b">текст[/b="x"]</strong></p>',
    );
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
  describe('[private="names"]', () => {
    it("converts to HTML with character marker", () => {
      const html = bbcodeToHtml(
        '[private="CharacterName"]secret message[/private]',
      );
      expect(html).toContain('data-bb-tag="private"');
      expect(html).toContain('data-bb-character="CharacterName"');
    });

    it("survives round-trip", () => {
      expectRoundTrip('[private="MyCharacter"]for character only[/private]');
    });

    it("handles names with spaces, quotes and commas", () => {
      expectRoundTrip('[private="John Smith"]message[/private]');
      expectRoundTrip('[private="Вася "Кот" Пупкин, Петя"]message[/private]');
    });
  });

  describe("[private] without names", () => {
    it("is a hidden block addressed to nobody", () => {
      const html = bbcodeToHtml("[private]secret[/private]");
      expect(html).toContain('data-bb-tag="private"');
      expect(html).not.toContain("data-bb-character");
      expectRoundTrip("[private]secret[/private]");
    });
  });

  // A name in capitals is the same name: the block is the one it would be in
  // small letters, and it comes back as typed.
  describe("in any case", () => {
    it("is the block its spelling in small letters is", () => {
      const html = bbcodeToHtml('[PRIVATE="Вася"]секрет[/Private] хвост', {
        context: "post",
      });
      expect(html).toContain('data-bb-character="Вася"');
      expect(html).not.toContain("data-bb-unclosed");
      expect(html.endsWith("</span> хвост</p>")).toBe(true);
      expectRoundTrip('[PRIVATE="Вася"]секрет[/Private] хвост');
      expectRoundTrip("[private]секрет[/PRIVATE] хвост");
    });
  });

  // A spelling the grammar does not know is still a hidden block, never text:
  // shown to the whole room it would publish the secret.
  describe("a broken spelling", () => {
    it.each([
      "[private=Вася]секрет[/private]",
      '[private to="Вася"]секрет[/private]',
      "текст [private",
    ])("stays hidden and comes back as typed: %s", (bbcode) => {
      const html = bbcodeToHtml(bbcode, { context: "post" });
      expect(html).toContain('data-bb-tag="private"');
      expect(html).not.toContain("data-bb-character");
      expectRoundTrip(bbcode);
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
// BLOCKS INSIDE BLOCKS
// ============================================================================

/**
 * Every pattern of a block stops at the first closing tag it meets, so a block
 * holding another one comes back crossed unless each closing tag is paired
 * with the element it closes. Text after the inner block is what shows it: a
 * crossed pair moves that text out of the outer block.
 */
describe("blocks inside blocks", () => {
  it.each([
    "[spoiler][nsfw]раз[/nsfw] два[/spoiler]",
    "[mod][spoiler]раз[/spoiler] два[/mod]",
    "[spoiler][quote]раз[/quote] два[/spoiler]",
    "[quote][spoiler]раз[/spoiler] два[/quote]",
    "[quote][nsfw][spoiler]раз[/spoiler] два[/nsfw] три[/quote] четыре",
  ])("come back nested: %s", (bbcode) => {
    expectRoundTrip(bbcode);
  });

  // The markup the server renders for the author, where every block is a div.
  const SERVER_BLOCKS: Array<[tag: string, opening: string]> = [
    ["spoiler", '<div class="spoiler" data-bb-tag="spoiler">'],
    ["nsfw", '<div class="nsfw-spoiler" data-bb-tag="nsfw">'],
    ["mod", '<div class="mod-block" data-bb-tag="mod">'],
    ["quote", '<div class="quote" data-bb-tag="quote">'],
  ];

  /** A nest of the blocks above, outermost first, with text after each. */
  function serverNest(depth: number): { html: string; bbcode: string } {
    let html = "текст";
    let bbcode = "текст";
    for (let level = depth - 1; level >= 0; level--) {
      const [tag, opening] = SERVER_BLOCKS[level % SERVER_BLOCKS.length];
      html = `${opening}${html} ${level}</div>`;
      bbcode = `[${tag}]${bbcode} ${level}[/${tag}]`;
    }
    return { html, bbcode };
  }

  // Innermost first by pattern took one block name per pass and gave up after
  // sixteen passes, and the order above, each block holding the one after it
  // in that list, is the order that costs the most of them.
  it("come back nested at any depth", () => {
    const { html, bbcode } = serverNest(40);
    expect(htmlToBbcode(html)).toBe(bbcode);
  });

  // The visual mode converts the whole text on every keystroke. Converting
  // innermost first by pattern restarted the scan after each block, so the time
  // grew with the square of their number. The HTML parser of the test
  // environment costs a good deal in itself, so rather than a ceiling the check
  // compares four times the blocks with the blocks: a square makes that
  // sixteen times dearer, one walk four.
  it("take one walk over the text however many there are", () => {
    const { html, bbcode } = serverNest(2);
    const cost = (count: number): number => {
      let best = Infinity;
      for (let run = 0; run < 2; run++) {
        const started = performance.now();
        htmlToBbcode(html.repeat(count));
        best = Math.min(best, performance.now() - started);
      }
      return best;
    };

    cost(250);
    const few = cost(1000);
    const many = cost(4000);

    // Blocks side by side come back a line apart.
    expect(htmlToBbcode(html.repeat(3))).toBe(Array(3).fill(bbcode).join("\n"));
    expect(many / few).toBeLessThan(8);
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
    expect(validateBBCode("[b]text")).toEqual([
      {
        reason: "unclosed",
        line: 1,
        text: "[b] не закрыт, добавьте [/b]",
        message: "Строка 1: [b] не закрыт, добавьте [/b]",
      },
    ]);
  });

  // A closing tag closes the nearest open tag of its name, and what was
  // opened inside it stays unclosed; the closing tag after it closes nothing.
  it("detects crossed tags", () => {
    expect(
      validateBBCode("[b][i]text[/b][/i]").map((issue) => issue.reason),
    ).toEqual(["unclosed", "stray-closing"]);
  });

  it("detects unexpected closing tags", () => {
    expect(validateBBCode("text[/b]")).toEqual([
      {
        reason: "stray-closing",
        line: 1,
        text: "[/b] ничего не закрывает, уберите его или откройте [b] выше",
        message:
          "Строка 1: [/b] ничего не закрывает, уберите его или откройте [b] выше",
      },
    ]);
  });

  it("names the line a finding is on", () => {
    expect(
      validateBBCode(
        "раз\nдва\n[img width=300]https://example.com/a.png[/img]",
      ),
    ).toEqual([
      {
        reason: "unquoted-value",
        line: 3,
        text: 'значение width пишется в прямых кавычках: width="..."',
        message:
          'Строка 3: значение width пишется в прямых кавычках: width="..."',
      },
    ]);
  });

  it("names a parameter as it was typed", () => {
    expect(
      validateBBCode('[img WIDTH="0"]https://example.com/a.png[/img]')[0].text,
    ).toBe("значение WIDTH должно быть целым числом от 1 до 9999");
  });

  it("is silent on what the editor's windows write", () => {
    for (const written of [
      '[link text="Главная"]https://example.com/[/link]',
      '[img width="300" height="200"]https://example.com/a.png[/img]',
      '[img height="200"]https://example.com/a.png[/img]',
      '[spoiler text="Карта"]текст[/spoiler]',
      '[private="Вася, Петя"]текст[/private]',
      "[private]текст[/private]",
      "[ul]\n[li]пункт[/li]\n[/ul]",
      "[code]текст[/code]",
      "[noparse][b]текст[/b][/noparse]",
    ]) {
      expect(validateBBCode(written, "post"), written).toEqual([]);
    }
  });

  // An image has no alt: a reader never sees it, and text nobody sees is
  // where an insult goes unnoticed. The page prints such a tag as typed.
  it("names an alt on an image, an empty one too", () => {
    for (const written of [
      '[img alt="кот"]https://example.com/a.png[/img]',
      '[img alt=""]https://example.com/a.png[/img]',
      '[img width="300" alt="кот"]https://example.com/a.png[/img]',
    ]) {
      expect(validateBBCode(written, "common"), written).toEqual([
        {
          reason: "unknown-param",
          line: 1,
          text: "у [img] нет параметра alt, есть только width и height",
          message:
            "Строка 1: у [img] нет параметра alt, есть только width и height",
        },
      ]);
    }
  });

  it("speaks Russian and names the reason", () => {
    const [issue] = validateBBCode('[code="x"]пример[/code]');
    expect(issue.reason).toBe("value-after-name");
    expect(issue.message).toBe(
      "Строка 1: у [code] нет значения, пишется просто [code]",
    );
  });

  it("handles complex valid nested structures", () => {
    expect(validateBBCode("[quote][b][i]text[/i][/b][/quote]")).toEqual([]);
  });

  it("has nothing to say about a name in capitals", () => {
    expect(
      validateBBCode("[B]текст[/b] [IMG]https://example.com/a.png[/IMG]"),
    ).toEqual([]);
  });

  // A tag the surface does not have is text there, as on the page, and the
  // check cannot know whether the author meant markup.
  it("has nothing to say about a tag the surface does not have", () => {
    expect(validateBBCode("[mod]текст", "post")).toEqual([]);
    expect(validateBBCode("[mod=1]текст[/mod]", "direct")).toEqual([]);
    expect(
      validateBBCode("[mod]текст", "common").map((issue) => issue.reason),
    ).toEqual(["unclosed"]);
  });

  // [code] only changes the font: left open, it is a tag left open, and the
  // hidden block after it is a hidden block the save takes.
  it("names a [code] left open and nothing after it", () => {
    expect(
      validateBBCode(
        '[code][b]x[/b]\n[private="Вася"]секрет[/private]',
        "post",
      ),
    ).toEqual([
      {
        reason: "unclosed",
        line: 1,
        text: "[code] не закрыт, добавьте [/code]",
        message: "Строка 1: [code] не закрыт, добавьте [/code]",
      },
    ]);
  });

  // Without its closing tag a [noparse] is no example: the [private] after it
  // is a hidden block like any other, and the save refuses it, since whoever
  // wrote it almost always meant an example.
  it("names a hidden block under a [noparse] left open", () => {
    expect(
      validateBBCode(
        '[noparse][b]x[/b]\n[private="Вася"]секрет[/private]',
        "post",
      ),
    ).toEqual([
      {
        reason: "unclosed",
        line: 1,
        text: "[noparse] не закрыт, добавьте [/noparse]",
        message: "Строка 1: [noparse] не закрыт, добавьте [/noparse]",
      },
      {
        reason: "private-spelling",
        line: 2,
        text: '[private="Вася"] не станет примером: [noparse] выше не закрыт тегом [/noparse]',
        message:
          'Строка 2: [private="Вася"] не станет примером: [noparse] выше не закрыт тегом [/noparse]',
      },
    ]);
  });

  // The save path reads no tag but [private] and [noparse], so it cannot tell
  // where the block around a [noparse] left open ends, and refuses every
  // hidden block after that [noparse]. The check names what the save will
  // refuse.
  it("names a hidden block after the block a [noparse] was left open in", () => {
    expect(
      validateBBCode(
        "[quote][noparse]x[/quote] [private]секрет[/private]",
        "post",
      ).map((issue) => issue.reason),
    ).toEqual(["unclosed", "private-spelling"]);
  });

  // A [/private] written as part of an example would end the block early and
  // show the rest, so the closing tag of a block opened before the [noparse]
  // is named too; a block opened after it is named once, at its opening.
  it("names the closing tag of a block the [noparse] was left open in", () => {
    expect(
      validateBBCode(
        '[private="Вася"]секрет [noparse]x\n[/private] хвост',
        "post",
      ).map((issue) => [issue.line, issue.text]),
    ).toEqual([
      [1, "[noparse] не закрыт, добавьте [/noparse]"],
      [
        2,
        "[/private] не станет примером: [noparse] выше не закрыт тегом [/noparse]",
      ],
    ]);
  });

  it("names a hidden block inside another", () => {
    expect(
      validateBBCode(
        '[private="Вася"]раз\n[private]два[/private][/private]',
        "post",
      ),
    ).toEqual([
      {
        reason: "private-nested",
        line: 2,
        text: "скрытый текст внутри скрытого не работает, закройте первый тегом [/private]",
        message:
          "Строка 2: скрытый текст внутри скрытого не работает, закройте первый тегом [/private]",
      },
    ]);
  });

  it("names a broken spelling of a hidden block as typed", () => {
    expect(
      validateBBCode(
        '[private=Вася]секрет[/private="x"][/private]',
        "post",
      ).map((issue) => issue.text),
    ).toEqual([
      '[private=Вася] записан неверно, скрытый текст пишется [private="Имя"] или [private]',
      '[/private="x"] не закрывает скрытый текст, закрывает только [/private]',
    ]);
    expect(validateBBCode("текст [private]секрет", "post")[0].text).toBe(
      "скрытый текст не закрыт, добавьте [/private]",
    );
  });

  it("cuts a long spelling at 40 characters", () => {
    const typed = `[private=${"а".repeat(50)}]`;
    expect(validateBBCode(`${typed}x[/private]`, "post")[0].text).toBe(
      `${typed.slice(0, 40)}... записан неверно, скрытый текст пишется [private="Имя"] или [private]`,
    );
  });

  // The page prints the tag as typed and the address in its place: the
  // author is told why before the send, not after.
  it("names an address the page refuses", () => {
    expect(
      validateBBCode("[link]mailto:a@example.com[/link]").map(
        (issue) => issue.text,
      ),
    ).toEqual(["адрес в [link] должен начинаться с http:// или https://"]);
    expect(
      validateBBCode("[img]http://192.168.0.1/a.png[/img]").map(
        (issue) => issue.text,
      ),
    ).toEqual([
      "адрес в [img] указывает на локальную сеть или записан с ошибкой",
    ]);
  });

  // Only [/private] ends a hidden block, so a closing tag of an outer tag
  // written inside one closes nothing; the check says where it has to go.
  it("names a closing tag that stands inside a hidden block", () => {
    expect(
      validateBBCode("[b]раз [private]два[/b] три[/private][/b]", "post").map(
        (issue) => issue.text,
      ),
    ).toEqual([
      "[/b] не закрывает [b] снаружи скрытого текста, перенесите его за [/private]",
    ]);
  });

  // The save path of every surface but the game post refuses a text with a
  // hidden block in it (PrivateBlockMarkup.DescribeSurfaceRefusal), and the
  // page erases such a block for every reader. The check used to read every
  // surface as a game post and said nothing about it.
  it("names a hidden block on a surface that has none, once", () => {
    expect(
      validateBBCode(
        'раз\n[private="Вася"]секрет[/private] [private]x',
        "common",
      ).map((issue) => issue.message),
    ).toEqual([
      "Строка 2: скрытый текст работает только в игровых постах, для примера оберните его в [noparse]",
      "Строка 2: скрытый текст не закрыт, добавьте [/private]",
    ]);
  });

  it("names a lone closing tag of a hidden block off its surface too", () => {
    expect(
      validateBBCode("текст [/private]", "direct").map((issue) => issue.reason),
    ).toEqual(["private-outside-game", "stray-closing"]);
  });

  it("leaves a hidden block alone in a game post and in an example", () => {
    expect(validateBBCode('[private="Вася"]секрет[/private]', "post")).toEqual(
      [],
    );
    expect(
      validateBBCode('[noparse][private="Вася"]x[/private][/noparse]', "info"),
    ).toEqual([]);
  });
});

// ============================================================================
// THE TEXT AS THE SERVER READS IT
// ============================================================================

// The server removes the control characters but tab, line feed and carriage
// return before it reads a text, and the removal joins what stood around one:
// "[nopa" U+0001 "rse]" is an example on the page. The editor used to read the
// stored text and drew a hidden block the page printed as an example to the
// whole room.
describe("the text the editor reads", () => {
  it("is read without the control characters, as the server reads it", () => {
    const html = bbcodeToHtml(
      '[nopa\u0001rse][private="Вася"]пример[/private][/noparse]',
      { context: "post" },
    );
    expect(html).toContain('data-bb-tag="noparse"');
    expect(html).not.toContain('data-bb-tag="private"');
    expect(validateBBCode('[private="Вася"]a[/priv\u0001ate] b')).toEqual([]);
  });

  it("keeps tab and line feed", () => {
    expect(htmlToBbcode(bbcodeToHtml("раз\tдва\nтри"))).toBe("раз\tдва\nтри");
  });

  // Built from code points: a file that carries U+FFFD itself fails
  // SourceEncodingShould, which reads it as the trace of a broken decoder.
  it("reads a surrogate with no partner as U+FFFD and keeps a pair", () => {
    const lone = String.fromCharCode(0xd800);
    const pair = String.fromCodePoint(0x1f600);
    const html = bbcodeToHtml(`а${lone}б ${pair}`);
    expect(html).toContain(`а${String.fromCharCode(0xfffd)}б ${pair}`);
    expect(html).not.toContain(lone);
  });
});

// ============================================================================
// CONTEXT TAGS STRUCTURE
// ============================================================================

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

// ============================================================================
// THE COST OF READING
// ============================================================================

/**
 * The reader is linear in the text, as the server's is (BbParserCostShould):
 * the visual mode reads the whole text on every switch and the markup check on
 * every pause in typing. Each form below makes a reader that looks for the end
 * of a tag by trying again from every position pay with the square of the
 * length; one that reads each character a bounded number of times pays four
 * times as much for four times the text.
 */
describe("the cost of reading", () => {
  const FORMS: Array<[string, (n: number) => string]> = [
    ["a name and spaces", (n) => "[img" + " ".repeat(n) + "x"],
    ["pairs of parameters", (n) => "[img" + ' a="1"'.repeat(n) + " x"],
    ["openings in a value", (n) => '[img alt="'.repeat(n) + "x"],
    ["quotes in a row", (n) => '[spoiler text="' + '"'.repeat(n) + " x"],
    ["hidden blocks never ended", (n) => '[private="a'.repeat(n)],
    ["tags never closed", (n) => "[b]".repeat(n)],
    ["closing tags with nothing open", (n) => "[/b]".repeat(n)],
    [
      "a verbatim block never closed",
      (n) => "[noparse]" + "[private ".repeat(n),
    ],
    // Every [noparse] looks ahead for its closing tag, past every "[/" there
    // is: a reader that looked again from each of them would pay the square.
    ["examples never closed", (n) => "[b][noparse][/b]".repeat(n)],
    [
      "closing tags inside a hidden block",
      (n) => "[b][private]" + "[/b]".repeat(n),
    ],
  ];

  /**
   * A read of the smaller text this slow is no linear reader, and timing the
   * larger one would take minutes: the run stops there and the form fails at
   * once. A linear reader gets through the smaller text two orders of
   * magnitude under it.
   */
  const CEILING_MS = 1_000;

  /**
   * The shortest time a ratio is taken against. A linear reader gets through
   * the smaller text in about that, and a run that short is mostly the noise
   * of whatever else the machine does: timed against itself, one pause of the
   * collector turned a linear reader into a square one, a different form on
   * every run. A square reader is far past the floor on the larger text, so
   * the floor costs the check nothing.
   */
  const FLOOR_MS = 5;

  const cost = (read: () => unknown): number => {
    let best = Infinity;
    for (let run = 0; run < 5; run++) {
      const started = performance.now();
      read();
      const took = performance.now() - started;
      best = Math.min(best, took);
      if (took > CEILING_MS) break;
    }
    return best;
  };

  const ratio = (
    read: (text: string) => unknown,
    small: string,
    large: string,
  ): number => {
    const few = cost(() => read(small));
    if (few > CEILING_MS) return Infinity;
    return cost(() => read(large)) / Math.max(few, FLOOR_MS);
  };

  it.each(FORMS)("grows with the text: %s", (_name, form) => {
    const small = form(10_000);
    const large = form(40_000);
    cost(() => bbcodeToHtml(small));

    // Four times the text; a square would make it sixteen times dearer.
    expect(ratio(bbcodeToHtml, small, large)).toBeLessThan(8);
    expect(ratio(validateBBCode, small, large)).toBeLessThan(8);
  });

  // Deeper than the server takes, the text is shown as typed (TreeMaxDepth).
  it("reads a tree deeper than the server takes as the text it is", () => {
    const deep = "[b]".repeat(600) + "x" + "[/b]".repeat(600);
    expect(bbcodeToHtml(deep)).toBe(`<p>${deep}</p>`);
    expect(validateBBCode(deep).map((issue) => issue.reason)).toEqual([
      "too-deep",
    ]);
    expectRoundTrip(deep);
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
    expectRoundTrip('[private="Character123"]text[/private]');
  });

  it("handles URLs with special characters", () => {
    expectRoundTrip(
      '[link text="link"]https://example.com/path?a=1&b=2[/link]',
    );
  });

  // Names compare in any case, as on the server, and the preview keeps the
  // spelling the author typed so that switching modes does not rewrite it.
  it("reads a tag written in capitals and gives it back as typed", () => {
    expect(bbcodeToHtml("[B]bold[/B]")).toBe(
      '<p><strong data-bb-tag="b" data-bb-open="[B]" data-bb-close="[/B]">bold</strong></p>',
    );
    expectRoundTrip("[B]bold[/B]");
    expect(bbcodeToHtml("[CODE][b]x[/b][/Code]")).toBe(
      '<p><code data-bb-tag="code" data-bb-open="[CODE]" data-bb-close="[/Code]"><strong data-bb-tag="b">x</strong></code></p>',
    );
    expectRoundTrip("[CODE][b]x[/b][/Code]");
    expectRoundTrip("[Quote]раз [I]два[/i][/QUOTE]");
    expectRoundTrip('[Spoiler TEXT="Карта"]текст[/SPOILER]');
    expectRoundTrip("[UL][Li]раз[/LI][/ul]");
    expectRoundTrip("раз[TAB]два");
  });

  // Only A-Z fold, and a name is read from A-Z and a-z alone. A letter that a
  // language's own case folding turns into a Latin one (toLowerCase and /iu
  // turn the Kelvin sign into k, toUpperCase and /iu the long s into S, and
  // .NET folds the Kelvin sign as well) ends the name instead, so the server
  // and the editor cannot read one text two ways.
  it("folds A-Z and nothing else", () => {
    expect(bbcodeToHtml("[LINK]https://example.com/[/LINK]")).toContain(
      'data-bb-tag="link"',
    );
    for (const typed of [
      "[lin\u212A]https://example.com/[/lin\u212A]",
      "[\u017Ftrike]x[/\u017Ftrike]",
      "[\u0130]x[/\u0130]",
    ]) {
      expect(bbcodeToHtml(typed), typed).toBe(`<p>${typed}</p>`);
      expect(validateBBCode(typed), typed).toEqual([]);
    }
  });

  // The editor writes a name in small letters when it builds the tag anew; a
  // spelling the author typed is kept only while it still names the tag.
  it("keeps a typed spelling only while it names the tag", () => {
    expect(
      htmlToBbcode(
        '<p><strong data-bb-tag="b" data-bb-open="[B]" data-bb-close="[/B]">x</strong></p>',
      ),
    ).toBe("[B]x[/B]");
    expect(
      htmlToBbcode(
        '<p><strong data-bb-tag="b" data-bb-open="[I]" data-bb-close="[/b\u212A]">x</strong></p>',
      ),
    ).toBe("[b]x[/b]");
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
  });

  // The server shows a link at http and https only (BbParserWrapper): an
  // address it drops must not pass here and vanish after the save.
  describe("addresses the server drops", () => {
    it.each([
      "mailto:user@example.com",
      "tel:+1234567890",
      "//example.com/path",
      "/path/to/page",
      "path/to/page",
      "https://exa mple.com/",
    ])("refuses %s", (url) => {
      expect(sanitizeUrl(url)).toBe("#");
    });

    // The reader's browser requests the address, so a post must not point it
    // at the reader's own machine or network.
    it.each([
      "http://localhost/",
      "http://a.localhost/",
      "http://127.0.0.1/",
      "http://127.1/",
      "http://2130706433/",
      "http://10.0.0.1/",
      "http://100.64.0.1/",
      "http://169.254.169.254/latest",
      "http://172.16.0.1/",
      "http://192.168.0.1/",
      "http://0.0.0.0/",
      "http://[::1]/",
      "http://[::ffff:127.0.0.1]/",
      "http://[fe80::1]/",
      "http://[fd00::1]/",
    ])("refuses the reader's own network: %s", (url) => {
      expect(sanitizeUrl(url)).toBe("#");
    });

    it.each([
      "http://8.8.8.8/",
      "http://172.32.0.1/",
      "http://[2001:db8::1]/",
      "https://example.com/",
    ])("lets a public address through: %s", (url) => {
      expect(sanitizeUrl(url)).toBe(url);
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

/**
 * An address the server refuses draws no image and no link: the tag is printed
 * as typed, its address in place, as the reader gets it. The author sees what
 * did not work, and the text comes back unchanged.
 */
describe("XSS Prevention - bbcodeToHtml", () => {
  /** Every element of the preview, and every address one of them carries. */
  const drawn = (bbcode: string) => {
    const template = document.createElement("template");
    template.innerHTML = bbcodeToHtml(bbcode);
    const elements = [...template.content.querySelectorAll("*")];
    return {
      tags: elements.map((element) => element.tagName),
      addresses: elements.flatMap((element) =>
        ["href", "src"].flatMap((name) => element.getAttribute(name) ?? []),
      ),
      text: template.content.textContent,
    };
  };

  describe("[link] XSS prevention", () => {
    it("draws no link for javascript: and prints the tag as typed", () => {
      const bbcode = '[link text="click me"]javascript:alert(1)[/link]';
      const { tags, addresses, text } = drawn(bbcode);
      expect(tags).not.toContain("A");
      expect(addresses).toEqual([]);
      expect(text).toBe(bbcode);
      expectRoundTrip(bbcode);
    });

    it("prints a link without text as typed for javascript:", () => {
      const bbcode = "[link]javascript:alert(1)[/link]";
      const { tags, addresses, text } = drawn(bbcode);
      expect(tags).not.toContain("A");
      expect(addresses).toEqual([]);
      expect(text).toBe(bbcode);
      expectRoundTrip(bbcode);
    });

    it("preserves safe links", () => {
      const html = bbcodeToHtml('[link text="safe"]https://example.com[/link]');
      expect(html).toContain('href="https://example.com"');
    });
  });

  describe("[img]URL[/img] XSS prevention (DM2 format)", () => {
    it("draws no image for javascript: and prints the tag as typed", () => {
      const bbcode = "[img]javascript:alert(1)[/img]";
      const { tags, addresses, text } = drawn(bbcode);
      expect(tags).not.toContain("IMG");
      expect(addresses).toEqual([]);
      expect(text).toBe(bbcode);
      expectRoundTrip(bbcode);
    });

    it("draws no image for data:", () => {
      const { tags, addresses } = drawn(
        '[img]data:image/svg+xml,<svg onload="alert(1)"/>[/img]',
      );
      expect(tags).not.toContain("IMG");
      expect(addresses).toEqual([]);
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

    it("preserves safe links from external HTML with their text", () => {
      const result = htmlToBbcode('<a href="https://safe.com">safe link</a>');
      expect(result).toBe('[link text="safe link"]https://safe.com[/link]');
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
  // The author's text comes back as it was: the server refuses the address
  // again when it renders the post, and nothing in between ever made it an
  // element with that address.
  it("dangerous links come back as the text the author typed", () => {
    const malicious = '[link text="click"]javascript:alert(1)[/link]';
    const html = bbcodeToHtml(malicious);
    expect(html).not.toMatch(/href="javascript/);
    expect(htmlToBbcode(html)).toBe(malicious);
  });

  it("dangerous images come back as the text the author typed", () => {
    const malicious = "[img]javascript:alert(1)[/img]";
    const html = bbcodeToHtml(malicious);
    expect(html).not.toMatch(/src="javascript/);
    expect(htmlToBbcode(html)).toBe(malicious);
  });

  it("safe URLs survive round-trip", () => {
    const safe = '[link text="safe link"]https://example.com[/link]';
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
   * The same image markup is emitted from three places — the server renderer,
   * this client renderer and the TipTap node — and they have drifted before:
   * one grew a data-alt the others never had. Freeze the attribute SET, not
   * the byte order, because the three legitimately order attributes
   * differently and a byte comparison would fail for the wrong reason.
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

describe("the private block the server renders", () => {
  // The author's own view of a post arrives as a div carrying
  // data-bb-addressees. Only the span the editor emits was recognised, so
  // opening a post for editing and saving it dropped the tag and published the
  // private text to the whole room.
  const AUTHOR_EDIT_HTML =
    '<div class="private-message" data-bb-tag="private" data-bb-addressees="Гэндальф">тайна</div>';

  it("survives being loaded into the editor and saved", () => {
    expect(htmlToBbcode(AUTHOR_EDIT_HTML)).toBe(
      '[private="Гэндальф"]тайна[/private]',
    );
  });

  it("survives without the marker attribute, by class alone", () => {
    const html =
      '<div class="private-message" data-bb-addressees="Гэндальф">тайна</div>';

    expect(htmlToBbcode(html)).toBe('[private="Гэндальф"]тайна[/private]');
  });

  it("still recognises the span the editor itself emits", () => {
    const html =
      '<span class="bb-private" data-bb-tag="private" data-bb-character="Гэндальф">тайна</span>';

    expect(htmlToBbcode(html)).toBe('[private="Гэндальф"]тайна[/private]');
  });

  // Empty addressees are the bare block, which the game leads alone read.
  it("reads empty addressees as the bare block", () => {
    const html =
      '<div class="private-message" data-bb-tag="private" data-bb-addressees="">тайна</div>';
    expect(htmlToBbcode(html)).toBe("[private]тайна[/private]");
  });

  // Addressees that cannot be written drop, never the block around them.
  it("keeps the block when the addressees cannot be written", () => {
    const html =
      '<div class="private-message" data-bb-tag="private" data-bb-addressees="Ва[ся">тайна</div>';
    expect(htmlToBbcode(html)).toBe("[private]тайна[/private]");
  });
});
