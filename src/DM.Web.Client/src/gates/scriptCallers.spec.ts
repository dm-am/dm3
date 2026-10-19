/**
 * @vitest-environment node
 */

/**
 * Every file under scripts/ and docker/scripts/ is called or named from
 * somewhere else in the repository: a script nobody calls is not run, and a
 * script that is not run stops being true. A call is a mention of the file in
 * any other committable file: its path from the root or a tail of two segments
 * or more, its name alone when no other file has that name, its path relative
 * to the caller, or a relative import without the extension; types beside a
 * module (x.d.mts next to x.mjs) are called by the module. A document naming
 * the file counts: the gate catches the file nobody names at all.
 */
import { describe, it, expect } from "vitest";
import { spawnSync } from "child_process";
import { existsSync, readFileSync } from "fs";
import { dirname, join, posix, resolve } from "path";
import { fileURLToPath } from "url";

// src/gates -> src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../..",
);

/** The tooling, and what the server runs from its clone of the repository. */
const SCRIPT = /^(?:scripts|docker\/scripts)\//;

/** Files that are not text, and so call nothing. */
const BINARY = /\.(?:png|jpe?g|gif|ico|ttf|woff2?)$/i;

/**
 * The register of the scripts names every one of them (scriptsReadme.spec.ts
 * holds that), so it would call each, and a script nobody else names would
 * pass this check by being listed.
 */
const REGISTER = "scripts/README.md";

/** A run of the characters a path is written in. */
const PATH_LIKE = /[\w./-]+/g;

/** The files a commit of the checkout would carry, by path from the root. */
const committable = (): string[] => {
  const listed = spawnSync(
    "git",
    ["ls-files", "-z", "--cached", "--others", "--exclude-standard"],
    { cwd: REPO_ROOT, encoding: "utf8", maxBuffer: 1 << 28 },
  );
  if (listed.status !== 0) {
    throw new Error(`git ls-files failed: ${listed.stderr}`);
  }
  // A tracked file deleted on disk goes with the next commit, not with this one.
  return listed.stdout
    .split("\0")
    .filter((path) => path && existsSync(join(REPO_ROOT, path)));
};

/** The path-like words of a text that hold a dot or a slash, trailing dots cut. */
const pathsIn = (text: string): Set<string> =>
  new Set(
    [...text.matchAll(PATH_LIKE)]
      .map(([word]) => word.replace(/\.+$/, ""))
      .filter((word) => /[./]/.test(word)),
  );

/** Every tail of a path by its segments, the path itself included. */
const tails = (path: string): string[] => {
  const segments = path.split("/").filter((s) => s && s !== "." && s !== "..");
  return segments.map((_, i) => segments.slice(i).join("/"));
};

const files = committable();
const scripts = files.filter((path) => SCRIPT.test(path));

const names = new Map<string, number>();
for (const path of files) {
  const name = posix.basename(path);
  names.set(name, (names.get(name) ?? 0) + 1);
}

/** Who writes each tail, and every path-like word of each file. */
const writers = new Map<string, Set<string>>();
const words = new Map<string, Set<string>>();
for (const file of files.filter(
  (path) => !BINARY.test(path) && path !== REGISTER,
)) {
  const found = pathsIn(readFileSync(join(REPO_ROOT, file), "utf8"));
  words.set(file, found);
  for (const word of found) {
    for (const tail of tails(word)) {
      let set = writers.get(tail);
      if (!set) writers.set(tail, (set = new Set()));
      set.add(file);
    }
  }
}

const callersOf = (script: string): string[] => {
  const segments = script.split("/");
  const name = segments.at(-1)!;
  const stem = name.replace(/\.[^.]+$/, "");
  const callers = new Set<string>();
  const take = (tail: string) =>
    writers.get(tail)?.forEach((file) => callers.add(file));

  for (let i = 0; i < segments.length - 1; i++) {
    take(segments.slice(i).join("/"));
  }
  if (names.get(name) === 1) take(name);

  // Relative to the caller: a file of the same folder, or an import that
  // leaves the extension to the resolver.
  const near = new Set([
    ...(writers.get(name) ?? []),
    ...(writers.get(stem) ?? []),
  ]);
  for (const file of near) {
    const relative = posix.relative(posix.dirname(file), script);
    const imported = relative.replace(/\.[^./]+$/, "");
    const spellings = [relative, `./${relative}`, `./${imported}`, imported];
    if (spellings.some((spelling) => words.get(file)?.has(spelling))) {
      callers.add(file);
    }
  }

  const module = script.replace(/\.d\.mts$/, ".mjs");
  if (module !== script && words.has(module)) callers.add(module);

  callers.delete(script);
  return [...callers];
};

describe("callers of the scripts", () => {
  it("reads the scripts and the files that call them", () => {
    // A walk that found nothing, or an index that holds nothing, would pass
    // the check below without having looked.
    expect(scripts.length).toBeGreaterThan(20);
    expect(callersOf("scripts/gates.sh")).toContain(
      ".github/workflows/dotnet.yml",
    );
    expect(callersOf("scripts/docs/lib/repo.mjs")).toContain(
      "scripts/docs/lib/router.mjs",
    );
  });

  it("does not take the register of the scripts for a caller", () => {
    expect(callersOf("scripts/gates.sh")).not.toContain(REGISTER);
  });

  it("names every script from some other file", () => {
    expect(scripts.filter((script) => callersOf(script).length === 0)).toEqual(
      [],
    );
  });
});
