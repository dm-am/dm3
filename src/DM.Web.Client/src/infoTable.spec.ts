/**
 * @vitest-environment node
 */

/**
 * The key-value fact table under an entity's title (the game's "Информация",
 * the blog's) is one design, not a shape every page redraws for itself. Two
 * pages held two copies of the rule, byte for byte identical except for a
 * single declaration, and that one difference was the whole of what a reader
 * saw: grey captions on the blog, body-coloured captions in the game.
 *
 * The rule lives in one mixin now. A page that opens its own `.info-table`
 * block for anything besides that call starts the drift over, so the block
 * may hold the call and nothing else.
 *
 * The check is over sources rather than over a rendered page: the colour is
 * a scoped stylesheet, and no runtime surface reaches every page that draws
 * the table.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const SELECTOR = ".info-table";
const MIXIN = "+info-table";

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (name.endsWith(".vue")) {
      out.push(full);
    }
  }
  return out;
}

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

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
  const blocks = collect(CLIENT_SRC).flatMap(blocksIn);

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
