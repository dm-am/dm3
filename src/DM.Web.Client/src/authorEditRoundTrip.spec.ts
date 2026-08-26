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

  /**
   * And declares the envelope it actually receives.
   *
   * Every single-resource answer of this API is `{ "resource": ... }`
   * (API_DESIGN.md: "Envelope<T> - стандарт для всех одиночных ресурсов"), and
   * the axios layer hands the body over untouched - unwrapResource is the
   * caller's job. A method declared with the bare payload type therefore lies
   * about its own answer, and the lie is silent: the field the editor reads is
   * undefined, the seeding falls through to whatever it had, and the editor
   * opens on the Display render or on nothing at all.
   *
   * That is not hypothetical. Three of these eight were declared bare: the game
   * post, the private message and the global chat message. The post one
   * published [private] text to the whole room on the first save, because what
   * the editor kept instead was the Display render, where the block is already
   * flattened into ordinary markup.
   *
   * The rule reads source text for the same reason the rule above does: the
   * declared type is the only thing these eight call sites share, and there is
   * nothing for TypeScript to check it against - the type argument is written
   * by hand and the server is not in the compilation.
   */
  it("declares the envelope the server actually sends", () => {
    const ENVELOPE =
      /Api\.(get|post|patch|put)<(Envelope|ListEnvelope|CursorEnvelope)</;
    const CALL = /Api\.(get|post|patch|put)</;
    const offenders: string[] = [];

    for (const file of sourceFiles(join(HERE, "entities"))) {
      if (!/api[\\/][a-zA-Z]+Api\.ts$/.test(file)) continue;
      const lines = readFileSync(file, "utf8").split("\n");

      for (let i = 0; i < lines.length; i++) {
        if (!AUTHOR_EDIT_CALL.test(lines[i])) continue;

        // The audience is an argument of the call, so the declaration is on
        // this line or a few above it - the formatter breaks the call across a
        // handful of lines at most.
        const opening = lines
          .slice(Math.max(0, i - 6), i + 1)
          .reverse()
          .find((line) => CALL.test(line));
        if (opening && ENVELOPE.test(opening)) continue;

        offenders.push(
          relative(HERE, file) +
            ":" +
            (i + 1) +
            " -> " +
            (opening ?? "?").trim(),
        );
      }
    }

    expect(offenders).toEqual([]);
  });
});
