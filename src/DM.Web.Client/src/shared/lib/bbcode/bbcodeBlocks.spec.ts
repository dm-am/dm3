/**
 * The tags of BLOCK_TAGS and blocks inside blocks, in the preview and on the
 * way back: BBCODE_RENDERING.md, "Набор тегов" and "Перевод строки после блока".
 */

import { describe, it, expect } from "vitest";
import { bbcodeToHtml } from "./bbcodeToHtml";
import { htmlToBbcode } from "./htmlToBbcode";
import {
  expectRoundTrip,
  expectToHtml,
} from "@/shared/lib/testing/converterRoundTrip";

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

  // Text inside a list but outside its items: the server lifts it out of the
  // list (TagNode.EmitList), the editor keeps the shape so the source survives
  // a round trip; both keep the text. Such a list is drawn as a block of what
  // it holds.
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

/**
 * A block inside another comes back crossed unless each closing tag is paired
 * with its element; text after the inner block shows it.
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

  // Converted on every keystroke: four times the blocks costs four times, not
  // sixteen. A ratio, since the test parser is slow in itself.
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
