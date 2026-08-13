import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, dirname, relative } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * An AuthorEdit response reaches the editor through htmlToBbcode.
 *
 * Both halves of the author-edit round trip are stated in the glossary: the
 * server returns HTML carrying data-bb-* attributes, the client sends BBCode
 * back. BbConverter performs the first half — it calls RenderHtml for every
 * audience except plain text. htmlToBbcode performs the second, and performing
 * it is the caller's job: BBCodeEditor takes and returns BBCode, and escapes
 * HTML handed to it.
 *
 * Skipping the conversion does not break loudly. The author sees markup instead
 * of text, the save still succeeds, and a private block that lost its wrapper is
 * published to every reader — the server escapes it on the way out like any
 * other text. This has happened once already: third-audit finding security-01
 * was closed by no longer seeding the editor from the Display render, but the
 * reverse conversion was added in one place out of eight, and the next edit went
 * the same way.
 *
 * The rule reads source text because nothing else proves it: each of the eight
 * call sites has its own screen, its own store and its own form, and the only
 * thing they share is the audience they ask for.
 */

const HERE = dirname(fileURLToPath(import.meta.url));

/** An API client call that declares the author's own audience. */
const AUTHOR_EDIT_CALL = /RENDER_AUDIENCE\.AuthorEdit/;

/** A method declared on an API client class. */
const PUBLIC_METHOD = /^\s*public\s+([a-zA-Z][a-zA-Z0-9]*)\s*\(/;

function sourceFiles(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules" || entry === "dist" || entry === "coverage")
      continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) sourceFiles(full, out);
    else if (/\.(ts|vue)$/.test(entry) && !/\.spec\.ts$/.test(entry))
      out.push(full);
  }
  return out;
}

/** API client methods that ask for the author's own audience. */
function authorEditMethods(): string[] {
  const found = new Set<string>();
  for (const file of sourceFiles(join(HERE, "entities"))) {
    if (!/api[\\/][a-zA-Z]+Api\.ts$/.test(file)) continue;
    const lines = readFileSync(file, "utf8").split("\n");
    let current: string | null = null;
    for (const line of lines) {
      const declared = PUBLIC_METHOD.exec(line);
      if (declared) current = declared[1];
      if (current && AUTHOR_EDIT_CALL.test(line)) {
        found.add(current);
        current = null;
      }
    }
  }
  return [...found];
}

describe("author-edit round trip", () => {
  const methods = authorEditMethods();

  it("finds the API methods that ask for the author's own rendering", () => {
    // A rule over an empty set passes, and the set is discovered by walking the
    // tree: renaming the wrapper must not leave this green over nothing.
    expect(methods.length).toBeGreaterThanOrEqual(6);
  });

  it("passes every response through htmlToBbcode before the editor sees it", () => {
    const offenders: string[] = [];

    for (const file of sourceFiles(HERE)) {
      const text = readFileSync(file, "utf8");
      // API client files declare these methods rather than consuming them.
      if (/api[\\/][a-zA-Z]+Api\.ts$/.test(file)) continue;

      // Consuming, not merely naming: a comment list hands the call down to a
      // child as a function (`const fetchEditSource = (id) => api.method(id)`)
      // and never sees the response — whoever awaits it unwraps it. Awaiting is
      // the marker that separates the reader from the forwarder.
      const calls = methods.filter((m) =>
        new RegExp("await\\s+[\\w.]*\\b" + m + "\\s*\\(").test(text),
      );
      if (!calls.length) continue;
      if (text.includes("htmlToBbcode")) continue;

      offenders.push(relative(HERE, file) + " -> " + calls.join(", "));
    }

    expect(offenders).toEqual([]);
  });
});
