// A side of a comparison built the way the client ships, into a directory of
// its own.
//
// Both sides are written to the same tree path in turn and built from there,
// so no absolute path can make two builds of one source differ. The tree sits
// under the client's node_modules/.cache: packages resolve upward from it the
// way they do from the client itself, no link to node_modules is needed, and
// the owner's dev server does not watch that directory.
//
// Every build runs in a process of its own. @vitejs/plugin-vue keeps its parsed
// components in a module-level cache keyed by file name, and a second build in
// the same process would read the first one's components back.

import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { CLIENT, UsageError, materializeClient } from "./git.mjs";

const HERE = dirname(fileURLToPath(import.meta.url));
const CHILD = join(HERE, "vite-build.mjs");
const CACHE = join(CLIENT, "node_modules", ".cache", "dm3-compare");

/** A fresh working directory for one run of a comparison tool. */
export function createRun() {
  if (!existsSync(join(CLIENT, "node_modules"))) {
    throw new UsageError(
      "the client has no node_modules: run npm ci in src/DM.Web.Client first",
    );
  }
  const run = join(CACHE, `${Date.now().toString(36)}-${process.pid}`);
  mkdirSync(run, { recursive: true });
  return run;
}

/** Removes a run directory, and refuses anything that is not one. */
export function removeRun(run) {
  if (relative(CACHE, run).startsWith("..") || dirname(run) !== CACHE) {
    throw new Error(
      `refusing to remove ${run}: not a run directory of ${CACHE}`,
    );
  }
  rmSync(run, { recursive: true, force: true });
}

/**
 * Builds the client as a side holds it and returns the output directory.
 *
 * `minify: false` turns both minifiers off. The production CSS minifier
 * (lightningcss) sorts declarations and merges rules, so a stylesheet compared
 * after it could not show a change of declaration order; the CSS gate builds
 * without it. The code comparison keeps the production minifier: what it
 * proves is that the shipped bundle did not change.
 *
 * Output names lose their content hash in both modes. A hash is a function of
 * the content, so it adds nothing to a content comparison, and a chunk whose
 * content did change would otherwise rename every chunk that imports it.
 */
export function buildSide(side, run, name, { minify, manifest = false }) {
  const tree = join(run, "tree");
  rmSync(tree, { recursive: true, force: true });
  mkdirSync(tree, { recursive: true });
  const files = materializeClient(side, tree);
  const outDir = join(run, `out-${name}`);
  const started = Date.now();
  process.stderr.write(`building ${side.label}: ${files} files...\n`);
  const result = spawnSync(
    process.execPath,
    [CHILD, JSON.stringify({ client: CLIENT, tree, outDir, minify, manifest })],
    { stdio: ["ignore", "inherit", "inherit"], windowsHide: true },
  );
  if (result.error) throw result.error;
  if (result.status !== 0) {
    throw new UsageError(
      `the build of ${side.label} failed (exit ${result.status})`,
    );
  }
  process.stderr.write(
    `built ${side.label} in ${Math.round((Date.now() - started) / 1000)}s\n`,
  );
  return outDir;
}

/** Every file of a build output, by path relative to it with forward slashes. */
export function readOutputs(outDir) {
  const found = new Map();
  const walk = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else
        found.set(
          relative(outDir, full).split(sep).join("/"),
          readFileSync(full),
        );
    }
  };
  walk(outDir);
  return new Map([...found].sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)));
}

/** Writes outputs read by readOutputs into a directory, for a saved baseline. */
export function writeOutputs(dir, outputs) {
  for (const [path, content] of outputs) {
    const full = join(dir, ...path.split("/"));
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, content);
  }
}
