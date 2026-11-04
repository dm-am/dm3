/**
 * [private] in the converter of the editor: names, no names, case and a
 * broken spelling (BBCODE_RENDERING.md, "Скрытый блок [private]").
 */

import { describe, it, expect } from "vitest";
import { bbcodeToHtml } from "./bbcodeToHtml";
import { htmlToBbcode } from "./htmlToBbcode";
import { expectRoundTrip } from "@/shared/lib/testing/converterRoundTrip";

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

describe("the private block the visual mode holds", () => {
  it("is read by the span the editor emits", () => {
    const html =
      '<span class="bb-private" data-bb-tag="private" data-bb-character="Гэндальф">тайна</span>';

    expect(htmlToBbcode(html)).toBe('[private="Гэндальф"]тайна[/private]');
  });

  // A pasted block that lost its marker attribute is still a block.
  it("is read by its class alone", () => {
    const html = '<div class="private-message">тайна</div>';

    expect(htmlToBbcode(html)).toBe("[private]тайна[/private]");
  });

  // Addressees that cannot be written drop, never the block around them.
  it("keeps the block when the addressees cannot be written", () => {
    const html =
      '<span class="bb-private" data-bb-tag="private" data-bb-character="Ва[ся">тайна</span>';
    expect(htmlToBbcode(html)).toBe("[private]тайна[/private]");
  });
});
