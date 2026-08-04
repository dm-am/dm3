/**
 * @vitest-environment node
 */

/**
 * A comment that names a file names one that exists.
 *
 * Whether the prose of a comment is true cannot be checked mechanically.
 * Whether the file it names exists can, and the name is what the reader acts
 * on first: they open it. Seventeen names in this tree pointed at nothing at
 * once. The last of them stood in the header of the topic card and sent the
 * reader to a component that had been renamed, and to a consumer that renders
 * a different card, so the edit the comment invited would have crossed the
 * boundary the other half of the code was built to hold.
 *
 * Only comments are read. Imports and route definitions name files too, but a
 * stale one there is a build error already.
 *
 * The extension list leaves out .json deliberately, because `response.json` is
 * also how a call reads, and a check that fails on working code teaches the
 * reader to switch it off.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { basename, dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(HERE, "..", "..", "..");

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage", "bin", "obj"]);

/** A file name, alone or at the end of a path. */
const NAMED =
  /(?:^|[^\w./\\-])((?:[\w.-]+\/)*[\w-]+\.(?:vue|tsx|ts|cs|scss|md|yml|cjs|props))(?![\w.-])/g;

const collect = (
  dir: string,
  wanted: (name: string) => boolean = () => true,
  out: string[] = [],
): string[] => {
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name)) continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) collect(full, wanted, out);
    else if (wanted(name)) out.push(full);
  }
  return out;
};

const asPath = (file: string): string =>
  relative(REPO_ROOT, file).split("\\").join("/");

/**
 * Comment text, line by line. String literals are skipped whole, so a URL in a
 * constant and a path handed to an import are not comments. A quote inside a
 * regular expression can swallow the comment that follows it, which costs a
 * missed check and never a false failure.
 */
const commentsOf = (source: string): { line: number; text: string }[] => {
  const pieces: { line: number; text: string }[] = [];
  let line = 1;
  let at = 0;

  const take = (text: string, from: number): void => {
    text.split("\n").forEach((piece, offset) => {
      if (piece.trim()) pieces.push({ line: from + offset, text: piece });
    });
  };

  while (at < source.length) {
    const here = source[at];
    const next = source[at + 1];

    if (here === "\n") {
      line++;
      at++;
    } else if (here === '"' || here === "'" || here === "`") {
      at++;
      while (at < source.length && source[at] !== here) {
        if (source[at] === "\\") at++;
        else if (source[at] === "\n") line++;
        at++;
      }
      at++;
    } else if (here === "/" && next === "/") {
      const end = source.indexOf("\n", at);
      const stop = end === -1 ? source.length : end;
      take(source.slice(at + 2, stop), line);
      at = stop;
    } else if (here === "/" && next === "*") {
      const end = source.indexOf("*/", at + 2);
      const stop = end === -1 ? source.length : end;
      const text = source.slice(at + 2, stop);
      take(text, line);
      line += text.split("\n").length - 1;
      at = stop + 2;
    } else if (source.startsWith("<!--", at)) {
      const end = source.indexOf("-->", at + 4);
      const stop = end === -1 ? source.length : end;
      const text = source.slice(at + 4, stop);
      take(text, line);
      line += text.split("\n").length - 1;
      at = stop + 3;
    } else {
      at++;
    }
  }

  return pieces;
};

describe("files named in comments", () => {
  const tracked = new Set(
    [
      ...["src", "test", "docs", "docker"].flatMap((top) =>
        collect(join(REPO_ROOT, top)),
      ),
      // The documents that live at the root of the tree.
      ...readdirSync(REPO_ROOT)
        .map((name) => join(REPO_ROOT, name))
        .filter((entry) => statSync(entry).isFile()),
    ].map(asPath),
  );
  const names = new Set([...tracked].map((path) => path.split("/").pop()!));

  /**
   * A name resolves as a whole path, as a file name anywhere in the tree, or
   * as the migration it is: EF puts a timestamp in front of a migration file,
   * and the timestamp is the part that would rot if a comment spelled it out.
   */
  const exists = (reference: string): boolean =>
    tracked.has(reference) ||
    names.has(basename(reference)) ||
    [...names].some((name) => name.endsWith(`_${basename(reference)}`)) ||
    [...tracked].some((path) => path.endsWith(`/${reference}`));

  const sources = [
    ...collect(join(REPO_ROOT, "src"), (name) =>
      /\.(vue|ts|cs|scss)$/.test(name),
    ),
    ...collect(join(REPO_ROOT, "test"), (name) => name.endsWith(".cs")),
  ];

  const dangling: string[] = [];
  let references = 0;
  for (const file of sources) {
    for (const { line, text } of commentsOf(readFileSync(file, "utf8"))) {
      for (const match of text.matchAll(NAMED)) {
        references++;
        if (!exists(match[1])) {
          dangling.push(`${asPath(file)}:${line} -> ${match[1]}`);
        }
      }
    }
  }

  it("reads the sources it is meant to read", () => {
    // A walk that found nothing, or a pattern that matched nothing, would pass
    // the assertion below without having looked at anything.
    expect(sources.length).toBeGreaterThan(1000);
    expect(references).toBeGreaterThan(100);
  });

  it("names only files this repository contains", () => {
    expect(dangling).toEqual([]);
  });
});
