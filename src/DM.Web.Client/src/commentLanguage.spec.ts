/**
 * @vitest-environment node
 */

/**
 * CODE_STYLE writes code and comments in English, and names the one layer that
 * steps aside: the operator files under docker, read by whoever brings a stand
 * up rather than by whoever writes the code. Everything under src and test
 * answers to the rule, and the rule had drifted - Russian comments in .cs, .ts
 * and .vue alike, in numbers nobody carries in their head.
 *
 * A comment that cites interface copy does not break it. "Отписаться" is the
 * word on the button, and a comment that translates it cuts the only link
 * between the two. So the rule is about the prose and not about the alphabet:
 * a Cyrillic run inside a comment stands in quotes, and the sentence around it
 * is English. Quotes are what tells a citation from a sentence, which is why
 * the exception is written that way rather than as a list of forgiven files -
 * a list forgives the next Russian paragraph in the same file too.
 *
 * Read out of the source, because a comment reaches no runtime surface. The
 * scan is line by line, with the two guards the ellipsis check uses: `//` opens
 * a comment outside a string literal and when it does not follow a colon, which
 * leaves "https://" alone in markup and in constants alike. A quote that opens
 * in the code of one line and closes in the code of another costs a missed
 * check and never a false failure, which is the only direction a rule like this
 * may be wrong in.
 *
 * Comments on touching lines are read as one, because a citation is regularly
 * split by the wrap and a quote opened on one line closes on the next.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(HERE, "..", "..", "..");

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
  // Its own budget, and a generous one. The default 15s is meant for a
  // behavioural test; this walks more than a thousand files and runs regexes
  // over every line of them, while two hundred other spec files share the
  // machine. Measured alone it takes under three seconds - the timeout is not
  // covering slowness here, it is refusing to call a busy machine a defect.
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
