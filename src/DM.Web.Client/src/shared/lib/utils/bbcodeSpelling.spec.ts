/**
 * How a tag is written: a value on a tag that takes none, a name in any case,
 * and the characters the server takes out (BBCODE_RENDERING.md, "Битый тег" and
 * "Регистр имен").
 */

import { describe, it, expect } from "vitest";
import { bbcodeToHtml } from "./bbcodeToHtml";
import { htmlToBbcode } from "./htmlToBbcode";
import { validateBBCode } from "./bbcodeWarnings";
import { expectRoundTrip } from "@/shared/lib/testing/converterRoundTrip";

/**
 * [code="x"], [b=1] and [quote="Имя"] are broken tags, shown as typed with what
 * they hold drawn as usual, [code] and [noparse] too; the editor hands them back
 * as typed.
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

describe("Edge Cases", () => {
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
