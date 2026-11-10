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
 *
 * scripts/ is held to the same limits: its TypeScript and JavaScript are linted
 * from the repository root with the client's config, and recorded under their
 * path from the root (scripts/...), which is how that run matches the records.
 */
import { describe, it, expect, beforeAll } from "vitest";
import { readdirSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath, pathToFileURL } from "url";
import { ESLint } from "eslint";

import {
  disagreements,
  measure,
  measurer,
  type FileSize,
  type Limits,
  type Measurement,
} from "@/shared/lib/testing/sizes";

/** The folder of this spec, src/gates/ of the client, where the register sits too. */
const HERE = dirname(fileURLToPath(import.meta.url));

/** The client folder. */
const CLIENT_DIR = resolve(HERE, "../..");

/** The repository root, the folder scripts/ is measured from. */
const ROOT = resolve(CLIENT_DIR, "../..");
const CONFIG = join(CLIENT_DIR, "eslint.config.js");
const SCRIPTS = ["scripts/**/*.{js,jsx,cjs,mjs,ts,tsx,cts,mts}"];

/** A record with the one line on why it stands, which the measuring never reads. */
type Explained<T> = T & { why?: string };
type FunctionRecord = Explained<NonNullable<FileSize["functions"]>[string]>;
type FileRecord = Explained<FileSize> & {
  functions?: Record<string, FunctionRecord>;
};

interface Ratchet {
  LIMITS: Limits;
  FUNCTION_SCOPE: string[];
  REGISTER: Record<string, FileRecord>;
}

// Read the way eslint.config.js reads it, as the module it is.
const { LIMITS, FUNCTION_SCOPE, REGISTER } = (await import(
  pathToFileURL(join(HERE, "sizeRatchet.js")).href
)) as Ratchet;

/** A whole-tree lint pass: half a minute on an idle machine, more under load. */
const WHOLE_TREE = 300_000;

let measured: Measurement;
let scripts: Measurement;
beforeAll(async () => {
  measured = await measure(
    measurer(CLIENT_DIR, LIMITS, FUNCTION_SCOPE),
    CLIENT_DIR,
  );
  scripts = await measure(
    measurer(ROOT, LIMITS, FUNCTION_SCOPE, CONFIG),
    ROOT,
    SCRIPTS,
  );
}, WHOLE_TREE);

const isSpec = (path: string) => /\.spec\.[^./]+$/.test(path);

const explained = (record: { why?: string }) =>
  typeof record.why === "string" && record.why.trim() !== "";

/**
 * The records of lines, complexity or a block that give no reason: a depth
 * record alone needs none, being what the tree held when depth arrived.
 */
function unexplained(register: Record<string, FileRecord>): string[] {
  const out: string[] = [];
  for (const [path, entry] of Object.entries(register)) {
    if ((entry.lines || entry.blocks) && !explained(entry)) {
      out.push(`${path}: its lines or blocks`);
    }
    for (const [name, size] of Object.entries(entry.functions ?? {})) {
      if ((size.lines || size.complexity) && !explained(size)) {
        out.push(`${path}: function "${name}"`);
      }
    }
  }
  return out;
}

/** What the register says one file is held to, rule by rule. */
function heldTo(path: string, entry: FileSize = {}) {
  const functions = Object.values(entry.functions ?? {});
  const most = (measure: "lines" | "complexity" | "depth", limit: number) =>
    Math.max(limit, ...functions.map((size) => size[measure] ?? 0));
  const rules: Record<string, unknown> = {
    "max-lines": [2, { max: entry.lines ?? LIMITS.lines }],
    complexity: [2, most("complexity", LIMITS.complexity)],
    "max-depth": [2, most("depth", Math.max(LIMITS.depth, entry.depth ?? 0))],
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
  "max-depth",
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
      const both = { ...measured.register, ...scripts.register };
      expect(disagreements(REGISTER, both)).toEqual([]);
    },
    WHOLE_TREE,
  );

  it("says why each record of lines, complexity or a block stands", () => {
    expect(unexplained(REGISTER)).toEqual([]);
  });

  it(
    "holds every linted file to the limits raised by its own records only",
    async () => {
      const client = new ESLint({ cwd: CLIENT_DIR });
      const root = new ESLint({ cwd: ROOT, overrideConfigFile: CONFIG });
      const wrong: string[] = [];
      const read = [
        ...measured.files.map((path) => ({ path, linter: client })),
        ...scripts.files.map((path) => ({ path, linter: root })),
      ];
      for (const { path, linter } of read) {
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

  it("measures every source under src/, e2e/, the client root and scripts/, none ignored", () => {
    const linted = new Set(measured.files);
    const lintedScripts = new Set(scripts.files);
    // The configs at the root are code an agent reads like any other.
    const root = readdirSync(CLIENT_DIR)
      .map((name) => join(CLIENT_DIR, name))
      .filter((full) => LINTED.test(full) && statSync(full).isFile());
    const unread = ["src", "e2e"]
      .flatMap((dir) => sources(join(CLIENT_DIR, dir)))
      .concat(root)
      .map((file) => relative(CLIENT_DIR, file).replace(/\\/g, "/"))
      .filter((path) => !linted.has(path))
      .concat(
        sources(join(ROOT, "scripts"))
          .map((file) => relative(ROOT, file).replace(/\\/g, "/"))
          .filter((path) => !lintedScripts.has(path)),
      );
    expect(unread).toEqual([]);
  });
});
