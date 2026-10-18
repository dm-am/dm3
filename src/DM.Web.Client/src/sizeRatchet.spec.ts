/**
 * @vitest-environment node
 */

/**
 * The size register of sizeRatchet.js against the tree, and the linter against
 * the register.
 *
 * Every record is the measured size, so the register is checked for equality
 * from both ends, the way SuppressedRulesShould checks its own: a measure past
 * its limit with no record fails, a record whose measure has grown fails, and
 * so does one whose measure has shrunk or dropped back under the limit, because
 * a record left above its file is room for the file to grow back into.
 *
 * The second half is what keeps the register the only way past a limit: every
 * file the linter reads is held to the limits of sizeRatchet.js, raised to its
 * own records and to nothing else. An "off", an ignores list or a hand-written
 * threshold for a size rule anywhere in eslint.config.js shows up here as a
 * file held to something the register does not say.
 */
import { describe, it, expect, beforeAll } from "vitest";
import { readdirSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath, pathToFileURL } from "url";
import { ESLint } from "eslint";

import {
  disagreements,
  measure,
  measurer,
  type FileSize,
  type Limits,
  type Measurement,
  type SizeRegister,
} from "@/shared/lib/testing/sizes";

/** The client folder: this spec sits in its src/. */
const CLIENT_DIR = dirname(dirname(fileURLToPath(import.meta.url)));

interface Ratchet {
  LIMITS: Limits;
  FUNCTION_SCOPE: string[];
  REGISTER: SizeRegister;
}

// Read the way eslint.config.js reads it, as the module it is.
const { LIMITS, FUNCTION_SCOPE, REGISTER } = (await import(
  pathToFileURL(join(CLIENT_DIR, "sizeRatchet.js")).href
)) as Ratchet;

/** A whole-tree lint pass: half a minute on an idle machine, more under load. */
const WHOLE_TREE = 300_000;

let measured: Measurement;
beforeAll(async () => {
  measured = await measure(
    measurer(CLIENT_DIR, LIMITS, FUNCTION_SCOPE),
    CLIENT_DIR,
  );
}, WHOLE_TREE);

const isSpec = (path: string) => /\.spec\.[^./]+$/.test(path);

/** What the register says one file is held to, rule by rule. */
function heldTo(path: string, entry: FileSize = {}) {
  const functions = Object.values(entry.functions ?? {});
  const most = (measure: "lines" | "complexity", limit: number) =>
    Math.max(limit, ...functions.map((size) => size[measure] ?? 0));
  const rules: Record<string, unknown> = {
    "max-lines": [2, { max: entry.lines ?? LIMITS.lines }],
    complexity: [2, most("complexity", LIMITS.complexity)],
  };
  if (!isSpec(path)) {
    rules["max-lines-per-function"] = [
      2,
      { max: most("lines", LIMITS.functionLines) },
    ];
  }
  if (path.endsWith(".vue")) {
    rules["vue/max-lines-per-block"] = [
      2,
      { ...LIMITS.blocks, ...entry.blocks },
    ];
  }
  return rules;
}

const SIZE_RULES = [
  "max-lines",
  "complexity",
  "max-lines-per-function",
  "vue/max-lines-per-block",
];

const LINTED = /\.(js|jsx|cjs|mjs|ts|tsx|cts|mts|vue)$/;

function sources(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) sources(full, out);
    else if (LINTED.test(name)) out.push(full);
  }
  return out;
}

describe("size ratchet", () => {
  it(
    "records every file, block and function past its limit at its size",
    () => {
      expect(disagreements(REGISTER, measured.register)).toEqual([]);
    },
    WHOLE_TREE,
  );

  it(
    "holds every linted file to the limits raised by its own records only",
    async () => {
      const linter = new ESLint({ cwd: CLIENT_DIR });
      const wrong: string[] = [];
      for (const path of measured.files) {
        const config = await linter.calculateConfigForFile(path);
        const rules = (config?.rules ?? {}) as Record<string, unknown>;
        const actual = Object.fromEntries(
          SIZE_RULES.filter((rule) => rules[rule] !== undefined).map((rule) => [
            rule,
            rules[rule],
          ]),
        );
        try {
          expect(actual).toEqual(heldTo(path, REGISTER[path]));
        } catch {
          wrong.push(`${path}: ${JSON.stringify(actual)}`);
        }
      }
      expect(wrong).toEqual([]);
    },
    WHOLE_TREE,
  );

  it("measures every source under src/ and e2e/ and at the root, none ignored", () => {
    const linted = new Set(measured.files);
    // The configs at the root are code an agent reads like any other.
    const root = readdirSync(CLIENT_DIR)
      .map((name) => join(CLIENT_DIR, name))
      .filter((full) => LINTED.test(full) && statSync(full).isFile());
    const unread = ["src", "e2e"]
      .flatMap((dir) => sources(join(CLIENT_DIR, dir)))
      .concat(root)
      .map((file) => relative(CLIENT_DIR, file).replace(/\\/g, "/"))
      .filter((path) => !linted.has(path));
    expect(unread).toEqual([]);
  });
});
