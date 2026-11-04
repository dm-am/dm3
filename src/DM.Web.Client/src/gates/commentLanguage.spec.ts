/**
 * @vitest-environment node
 */

/**
 * CODE_STYLE writes code and comments in English; the operator files under
 * docker step aside. A comment that cites interface copy does not break the
 * rule: "Отписаться" is the word on the button, and translating it cuts the
 * link between the two. So the rule is about the prose: a Cyrillic run inside a
 * comment stands in quotes, and the sentence around it is English. Comments on
 * touching lines are read as one, since a citation is often split by the wrap.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { join, relative, resolve } from "path";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";

// src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(CLIENT_SRC, "..", "..", "..");

/** U+0400..U+04FF, by code point so this file stays clean of them itself. */
const CYRILLIC = /[\u0400-\u04FF]/;

/** A citation: the copy quoted as the screen spells it. */
const QUOTED = /"[^"]*"|`[^`]*`/g;

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage", "bin", "obj"]);

/** The three languages this tree is written in. */
const SOURCE = /\.(cs|ts|vue)$/;

/** Block comments, opener and closer: the C-family one and the markup one. */
const BLOCKS: [string, string][] = [
  ["/*", "*/"],
  ["<!--", "-->"],
];

type CommentLine = { line: number; text: string };

/** Whether a marker at this position stands outside a string literal. */
const outsideALiteral = (line: string, at: number): boolean => {
  const before = line.slice(0, at);
  const unclosed = (quote: string): boolean =>
    (before.split(quote).length - 1) % 2 === 1;
  return !unclosed('"') && !unclosed("'") && !unclosed("`");
};

/** Every comment of a file, one entry per line it covers. */
const commentsOf = (source: string): CommentLine[] => {
  const found: CommentLine[] = [];
  let closer: string | null = null;

  source.split("\n").forEach((raw, index) => {
    const line = index + 1;
    let start = closer ? 0 : -1;
    let at = 0;

    while (at < raw.length) {
      if (closer !== null) {
        const end = raw.indexOf(closer, at);
        if (end < 0) break;
        at = end + closer.length;
        found.push({ line, text: raw.slice(start, at) });
        closer = null;
        start = -1;
        continue;
      }

      if (
        raw.startsWith("//", at) &&
        raw[at - 1] !== ":" &&
        outsideALiteral(raw, at)
      ) {
        found.push({ line, text: raw.slice(at) });
        return;
      }

      const block = BLOCKS.find(([opener]) => raw.startsWith(opener, at));
      if (block && outsideALiteral(raw, at)) {
        closer = block[1];
        start = at;
        at += block[0].length;
        continue;
      }

      at += 1;
    }

    if (closer !== null && start >= 0) {
      found.push({ line, text: raw.slice(start) });
    }
  });

  return found;
};

/** Comments on consecutive lines, read as one. */
const blocksOf = (comments: CommentLine[]): CommentLine[][] => {
  const blocks: CommentLine[][] = [];
  for (const comment of comments) {
    const open = blocks[blocks.length - 1];
    if (open && open[open.length - 1].line === comment.line - 1) {
      open.push(comment);
    } else {
      blocks.push([comment]);
    }
  }
  return blocks;
};

/** What a block says in its own voice: the citations taken out. */
const proseOf = (block: CommentLine[]): string =>
  block
    .map((comment) => comment.text)
    .join("\n")
    .replace(QUOTED, "");

const collect = (dir: string, found: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name)) continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) collect(full, found);
    else if (SOURCE.test(name)) found.push(full);
  }
  return found;
};

const violationsIn = (file: string): string[] => {
  const source = readFileSync(file, "utf8");
  if (!CYRILLIC.test(source)) return [];
  const path = relative(REPO_ROOT, file).split("\\").join("/");
  return blocksOf(commentsOf(source))
    .filter((block) => CYRILLIC.test(proseOf(block)))
    .map((block) => `${path}:${block[0].line}: ${block[0].text.trim()}`);
};

describe("comments are written in English", () => {
  // A walk of every source file on a busy machine, not a slow test.
  it(
    "holds across src and test, the copy they cite excepted",
    { timeout: 120_000 },
    () => {
      const files = ["src", "test"].flatMap((tree) =>
        collect(join(REPO_ROOT, tree)),
      );
      // A walk that finds nothing passes.
      expect(files.length).toBeGreaterThan(1000);
      expect(files.flatMap(violationsIn)).toEqual([]);
    },
  );
});
