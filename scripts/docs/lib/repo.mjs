// Where the tree is, how it is walked and read, and where the compiler comes from.
//
// Every walk is sorted by code unit, and every path leaves here with forward
// slashes, so the document comes out the same on Windows and in CI.
import { readFileSync, readdirSync, statSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

export const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

export const CLIENT = join(ROOT, "src", "DM.Web.Client");

/** Directory names no walk descends into: build output and installed packages. */
const SKIPPED = new Set(["node_modules", "dist", "coverage", "bin", "obj", ".vite"]);

/** Repo-relative, with forward slashes. */
export function rel(path) {
  return relative(ROOT, path).split(sep).join("/");
}

/** Text of a file with its line endings made LF. */
export function read(path) {
  return readFileSync(path, "utf8").replace(/\r\n/g, "\n");
}

/** Every file under a directory the predicate accepts, sorted. */
export function walk(directory, accept) {
  const found = [];
  const visit = (current) => {
    const entries = readdirSync(current).sort(compare);
    for (const entry of entries) {
      const full = join(current, entry);
      if (statSync(full).isDirectory()) {
        if (!SKIPPED.has(entry)) visit(full);
      } else if (accept(full)) {
        found.push(full);
      }
    }
  };
  visit(directory);
  return found;
}

/** Code-unit order: the same on every machine, unlike localeCompare. */
export function compare(a, b) {
  if (a < b) return -1;
  if (a > b) return 1;
  return 0;
}

/**
 * The TypeScript compiler the client installs. The script carries no package of
 * its own, so it borrows that one; without `npm ci` in the client there is
 * nothing to parse with, and the run stops saying so instead of guessing.
 */
export function loadTypeScript() {
  const require = createRequire(join(CLIENT, "package.json"));
  try {
    return require("typescript");
  } catch {
    process.stderr.write(
      "typescript is not installed: run npm ci in src/DM.Web.Client first\n",
    );
    process.exit(2);
  }
}
