/**
 * The two checks the converter's specs share: a text survives the preview and
 * back, and draws the HTML expected of it (TipTap is bbcode-roundtrip.spec.ts's).
 */

import { expect } from "vitest";
import { bbcodeToHtml } from "@/shared/lib/bbcode/bbcodeToHtml";
import { htmlToBbcode } from "@/shared/lib/bbcode/htmlToBbcode";

/**
 * Test that BBCode survives round-trip conversion
 */
export function expectRoundTrip(bbcode: string) {
  const html = bbcodeToHtml(bbcode);
  const result = htmlToBbcode(html);
  expect(result).toBe(bbcode);
}

/**
 * Test that BBCode converts to expected HTML
 */
export function expectToHtml(bbcode: string, expectedHtml: string) {
  expect(bbcodeToHtml(bbcode)).toBe(expectedHtml);
}
