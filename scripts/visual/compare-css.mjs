#!/usr/bin/env node
// The CSS equivalence gate: builds the client twice and compares the styles
// the browser would get: every stylesheet, the sheets and <style> blocks of
// each page, and the order in which each chunk brings its sheets.
//
// It reads styles only. For an edit of styles alone (Sass and CSS files, the
// style blocks of components, mixins, partials, comments in styles) a pass
// here stands in for the frame stand. An edit that also touches markup or
// scripts does not qualify by this gate alone: moving a component to another
// folder changes imports and barrels, and needs `compare-code.mjs --build`
// to pass as well, which compares the scripts and the page of the build. A
// change of behaviour still needs frames and a live stand.
//
//   node scripts/visual/compare-css.mjs                      HEAD against the working tree
//   node scripts/visual/compare-css.mjs --base REV --head REV
//   node scripts/visual/compare-css.mjs --head-dir DIR       against a copy laid out like the repository
//   node scripts/visual/compare-css.mjs --save-base FILE [--base-only]
//   node scripts/visual/compare-css.mjs --baseline FILE      reuse a saved base instead of building it
//
// A directory side leaves out what git would ignore at the same path in the
// repository (a .env.local, build output). --keep leaves the build
// directories under the client's node_modules/.cache/dm3-compare for
// inspection.
//
// What is compared and what is normalized is written down in
// src/DM.Web.Client/src/shared/lib/testing/cssSnapshot.ts. In short: only
// whitespace, comments, the optional last semicolon of a block and scope ids
// are forgiven; the order of declarations, rules and sheets is compared as
// built.
//
// Exit code: 0 the same CSS, 1 different, 2 the comparison could not be made.

import { createHash } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import {
  checkBaselineOptions,
  readArguments,
  runTool,
} from "./compare/args.mjs";
import { buildSide, createRun, removeRun } from "./compare/build.mjs";
import { CLIENT, UsageError } from "./compare/git.mjs";

const SNAPSHOT_MODULE = join(
  CLIENT,
  "src",
  "shared",
  "lib",
  "testing",
  "cssSnapshot.ts",
);
const SHOWN = 20;

async function main() {
  const options = readArguments(process.argv.slice(2), {
    values: ["--save-base", "--baseline"],
    flags: ["--keep", "--base-only"],
  });
  if (options.paths.length > 0) {
    throw new UsageError(
      "the CSS gate compares whole builds and takes no paths",
    );
  }
  checkBaselineOptions(options);
  const { SECTION, cssSnapshot, compareSnapshots } = await import(
    pathToFileURL(SNAPSHOT_MODULE).href
  );
  // A saved base holds normalized lines, so it is only comparable with lines
  // the same normalizer produced: the file is stamped with its hash.
  const stamp = `### gate ${createHash("sha1").update(readFileSync(SNAPSHOT_MODULE)).digest("hex")}`;
  const run = createRun();
  try {
    let base;
    let baseLabel;
    if (options.baseline) {
      const [header, ...lines] = readFileSync(options.baseline, "utf8").split(
        "\n",
      );
      if (header !== stamp) {
        throw new UsageError(
          `${options.baseline} was saved by another version of cssSnapshot.ts; save the base again`,
        );
      }
      base = lines;
      baseLabel = `baseline ${options.baseline}`;
    } else {
      base = cssSnapshot(
        buildSide(options.baseSide, run, "base", {
          minify: false,
          manifest: true,
        }),
      );
      baseLabel = options.baseSide.label;
      if (options["save-base"]) {
        writeFileSync(options["save-base"], [stamp, ...base].join("\n"));
        console.log(`saved the CSS of ${baseLabel} to ${options["save-base"]}`);
        if (options["base-only"]) return true;
      }
    }
    const head = cssSnapshot(
      buildSide(options.headSide, run, "head", {
        minify: false,
        manifest: true,
      }),
    );
    const differences = compareSnapshots(base, head);
    const sections = head.filter((line) => line.startsWith(SECTION)).length;
    console.log(`CSS gate: base ${baseLabel}, head ${options.headSide.label}`);
    if (differences.length === 0) {
      console.log(
        `same CSS: ${sections} sections (sheets, chunk sheet orders, page styles) equal`,
      );
      return true;
    }
    console.log(
      `different CSS: ${differences.length} of ${sections} sections differ`,
    );
    for (const difference of differences.slice(0, SHOWN)) {
      const where =
        difference.change === "changed"
          ? ` at line ${difference.line + 1}`
          : "";
      console.log(`\n  ${difference.section}: ${difference.change}${where}`);
      for (const line of difference.before) console.log(`      ${line}`);
      for (const line of difference.base) console.log(`    - ${line}`);
      for (const line of difference.head) console.log(`    + ${line}`);
    }
    if (differences.length > SHOWN)
      console.log(`\n  ...and ${differences.length - SHOWN} more`);
    return false;
  } finally {
    if (options.keep) console.log(`kept ${run}`);
    else removeRun(run);
  }
}

await runTool(main);
