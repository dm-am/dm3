import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/**
 * The server gives the author the BBCode as typed (the author_edit audience),
 * so an edit is seeded with the text of the answer and nothing else. The way
 * back from HTML belongs to the editor alone, which turns its visual mode into
 * BBCode before the text leaves it. Run over the server's answer, the
 * converter reads the typed brackets as text and the author's markup changes
 * under the save (BBCODE_RENDERING.md, "Исходник на правку"). The rule reads
 * source text: the call sites share nothing but the import.
 */

/** An import of the way back from HTML, by alias or by a relative path. */
const IMPORTS_CONVERTER = /from\s+["'][^"']*\/htmlToBbcode["']/;

/** Where the way back may be read: the editor and the spec-only helpers. */
const ALLOWED = [/^shared\/ui\/BBCodeEditor\//, /^shared\/lib\/testing\//];

const importers = (): string[] =>
  collect(
    CLIENT_SRC,
    (name) => /\.(ts|vue)$/.test(name) && !/\.spec\.ts$/.test(name),
  )
    .filter((file) => IMPORTS_CONVERTER.test(readFileSync(file, "utf8")))
    .map(asPath);

describe("the author's source", () => {
  const found = importers();

  it("finds the editor among the importers of the converter", () => {
    // A rule over an empty set passes: renaming the module must not leave
    // this green over nothing.
    expect(found).toContain("shared/ui/BBCodeEditor/BBCodeEditor.vue");
  });

  it("reaches the editor as the server sent it, past no converter", () => {
    expect(
      found.filter((file) => !ALLOWED.some((allowed) => allowed.test(file))),
    ).toEqual([]);
  });
});
