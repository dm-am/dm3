/**
 * @vitest-environment node
 */

// The bar copies the line of the record's text so the log does not jump; this holds the copy.
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

    // The text runs in the page's type, so an em of the bar is an em of the text only there.
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
