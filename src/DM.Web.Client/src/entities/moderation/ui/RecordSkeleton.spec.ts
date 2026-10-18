/**
 * @vitest-environment node
 */

/**
 * The waiting twin of a record stands in the geometry of the record, so the
 * log does not jump when the answer replaces it. The bar of the text is one
 * line of the record's text: the text took DM2's step of 20 (line-height
 * 1.25 on the page's 16px, RecordBlock.vue), the bar stayed at 1.6em, and it
 * stood 5.6px taller than the line it waits for.
 *
 * Read off the compiled styles of both files: jsdom lays nothing out, and the
 * two numbers live in two components that change apart.
 */
import { describe, it, expect } from "vitest";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  componentCss,
  ruleMap,
} from "@/shared/lib/testing/styles";

const HERE = dirname(fileURLToPath(import.meta.url));

describe("RecordSkeleton", () => {
  it("draws the bar of the text one line of the record's text tall", () => {
    const skeleton = ruleMap(componentCss(resolve(HERE, "RecordSkeleton.vue")));
    const text = ruleMap(componentCss(resolve(HERE, "RecordBlock.vue"))).get(
      ".record-block__text",
    );
    const pageType = ruleMap(compileSass(".page\n  font-size: $font-size")).get(
      ".page",
    )?.["font-size"];

    // The record's text sets no size of its own and runs in the page's type,
    // so an em of the bar is an em of the text only in that type.
    expect(text?.["font-size"]).toBeUndefined();
    expect(skeleton.get(".skeleton-text")?.["font-size"]).toBe(pageType);

    expect(text?.["line-height"], "the record's text sets its line").toBe(
      "1.25",
    );
    expect(skeleton.get(".skeleton-text")?.height).toBe(
      `${text?.["line-height"]}em`,
    );
  });
});
