/**
 * @vitest-environment node
 */

/**
 * The key-value fact table under an entity's title (the game's "Информация",
 * the blog's) is one design: the rule lives in one mixin, and a page's own
 * `.info-table` block may hold the call and nothing else, since a copy of the
 * rule drifts one declaration at a time. Read off sources: no runtime surface
 * reaches every page that draws the table.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { parse as parseSfc } from "vue/compiler-sfc";
import { compileSass, ruleMap } from "@/shared/lib/testing/styles";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

const SELECTOR = ".info-table";
const MIXIN = "+info-table";

const isComponent = (name: string): boolean => name.endsWith(".vue");

const indentOf = (line: string): number =>
  line.length - line.trimStart().length;

/** Every `.info-table` block in a file, as the lines it declares. */
function blocksIn(file: string): { path: string; body: string[] }[] {
  const { descriptor } = parseSfc(readFileSync(file, "utf8"), {
    filename: file,
  });
  const found: { path: string; body: string[] }[] = [];

  for (const style of descriptor.styles) {
    if (style.lang !== "sass") continue;
    const lines = style.content.split("\n");
    lines.forEach((raw, index) => {
      if (raw.trim() !== SELECTOR) return;
      const indent = indentOf(raw);
      const body: string[] = [];
      for (const next of lines.slice(index + 1)) {
        const text = next.trim();
        if (!text) continue;
        if (indentOf(next) <= indent) break;
        if (text.startsWith("//")) continue;
        body.push(text);
      }
      found.push({ path: asPath(file), body });
    });
  }
  return found;
}

describe("the entity info table", () => {
  const blocks = collect(CLIENT_SRC, isComponent).flatMap(blocksIn);

  it("is drawn by more than one page, or this test guards nothing", () => {
    expect(blocks.length).toBeGreaterThan(1);
  });

  it("takes its whole look from the one mixin", () => {
    const offenders = blocks
      .filter((block) => block.body.join(" ") !== MIXIN)
      .map((block) => `${block.path}: ${block.body.join(" / ")}`);
    expect(offenders).toEqual([]);
  });
});

/** A caption stands in one line, as wide as it is, at every width. */
describe("a caption of the entity info table", () => {
  const css = compileSass(
    `@use "@/assets/styles/Tables" as *\n.info-table\n  ${MIXIN}`,
  );

  it("stands in one line as wide as it is, at every width", () => {
    expect(ruleMap(css).get(`${SELECTOR} th`)).toMatchObject({
      width: "1%",
      "white-space": "nowrap",
    });
    expect(css).not.toMatch(/@media/);
  });
});
