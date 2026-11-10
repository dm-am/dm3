/**
 * @vitest-environment node
 */

/**
 * The size limit of the stylesheets, which ESLint does not read: a style sheet
 * may be 400 lines. A sheet past it is split, not recorded: an exception takes
 * the owner's word and brings a register of exceptions back into this file.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

import { HOW_TO_CUT } from "@/shared/lib/testing/sizes";

const CLIENT_SRC = dirname(dirname(fileURLToPath(import.meta.url)));

const LIMIT = 400;

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

describe("stylesheet size limit", () => {
  it("keeps every sheet within the limit", () => {
    const past = sheets(CLIENT_SRC)
      .map((file) => ({
        path: relative(CLIENT_SRC, file).replace(/\\/g, "/"),
        length: lengthOf(file),
      }))
      .filter(({ length }) => length > LIMIT)
      .map(
        ({ path, length }) =>
          `${path}: ${length} lines is past ${LIMIT}: split the sheet; ${HOW_TO_CUT}`,
      );

    expect(past).toEqual([]);
  });
});
