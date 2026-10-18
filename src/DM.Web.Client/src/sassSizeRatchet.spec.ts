/**
 * @vitest-environment node
 */

/**
 * The size ratchet of the stylesheets, which ESLint does not read: a style
 * sheet may be 400 lines, and the ones that were longer on the day the limit
 * arrived are held at the length they had then.
 *
 * Same contract as sizeRatchet.js and its spec. A record is the measured
 * length, checked for equality: growth fails, and so does shrinkage, since a
 * record left above its file is room for the file to grow back into. A sheet
 * past the limit with no record fails, and a record with no sheet behind it
 * fails as stale. The register only shrinks: a new record widens the
 * exceptions and takes the owner's word, and the lawful move is to split the
 * sheet.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const LIMIT = 400;

/** Path from src/ -> recorded length in lines. */
const RECORDED: Record<string, number> = {
  "assets/styles/AuthSurface.sass": 870,
  "assets/styles/Inputs.sass": 585,
  "assets/styles/_BbcodeContent.sass": 434,
  "assets/styles/_Filters.sass": 575,
};

const SHEET = /\.(sass|scss|css)$/;

function sheets(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) sheets(full, out);
    else if (SHEET.test(name)) out.push(full);
  }
  return out;
}

/** Counted the way ESLint's max-lines counts: a final newline adds no line. */
function lengthOf(file: string): number {
  const lines = readFileSync(file, "utf8").split(/\r?\n/);
  if (lines.length > 1 && lines.at(-1) === "") lines.pop();
  return lines.length;
}

describe("stylesheet size ratchet", () => {
  it("records every sheet past the limit at its length, and nothing else", () => {
    const measured = new Map(
      sheets(CLIENT_SRC)
        .map(
          (file) =>
            [
              relative(CLIENT_SRC, file).replace(/\\/g, "/"),
              lengthOf(file),
            ] as const,
        )
        .filter(([, length]) => length > LIMIT),
    );
    const problems: string[] = [];
    for (const path of new Set([
      ...Object.keys(RECORDED),
      ...measured.keys(),
    ])) {
      const record = RECORDED[path];
      const length = measured.get(path);
      if (record === length) continue;
      if (record === undefined) {
        problems.push(
          `${path}: ${length} lines is past ${LIMIT} and has no record: split the sheet (a new record needs the owner's word)`,
        );
      } else if (length === undefined) {
        problems.push(
          `${path}: back within ${LIMIT} lines or gone: drop the record of ${record}`,
        );
      } else if (length > record) {
        problems.push(`${path}: grew from ${record} to ${length} lines`);
      } else {
        problems.push(
          `${path}: shrank from ${record} to ${length} lines: lower the record to ${length}`,
        );
      }
    }
    expect(problems).toEqual([]);
  });
});
