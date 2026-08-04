/**
 * @vitest-environment node
 */

/**
 * Two lines of the build config described something the build did not do.
 *
 * `chunkSizeWarningLimit: 500` sat under "Увеличим лимит предупреждения о
 * размере chunk" and 500 kB is vite's own default, so the line raised nothing
 * and left the impression that large-chunk warnings had been deliberately
 * silenced. And the editor chunk was labelled "загружается только на
 * chat/messenger" while two dozen views import the editor - forum, blogs,
 * games, profile, moderation, support. The next person tuning load time would
 * have looked for a win where there is none.
 *
 * A comment cannot be checked against reality in general. These two can: one
 * names a number, the other names a set of pages.
 *
 * The third check is about the language they are written in. The project's rule
 * is that documentation is Russian and code is English, and this file is code:
 * the chunking notes were the last Russian ones left in it, next to English
 * paragraphs added later, so one file explained itself in two languages.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client
const CLIENT_ROOT = resolve(HERE, "..");
const CONFIG = readFileSync(join(CLIENT_ROOT, "vite.config.ts"), "utf8");

/** vite's own default, in kB. Setting it to this changes nothing. */
const VITE_DEFAULT_CHUNK_WARNING = 500;

describe("vite.config.ts", () => {
  it("does not set the chunk warning threshold to vite's default", () => {
    const match = CONFIG.match(/chunkSizeWarningLimit:\s*(\d+)/);
    if (match === null) return;
    expect(
      Number(match[1]),
      "a threshold equal to the default reads as a decision and is not one: raise it deliberately or drop the line",
    ).not.toBe(VITE_DEFAULT_CHUNK_WARNING);
  });

  it("does not claim the editor chunk is limited to a couple of pages", () => {
    expect(
      /chat\s*\/\s*messenger/i.test(CONFIG),
      "the editor is imported across the forum, blogs, games, profile, moderation and support: naming two sections here is a false premise for the next chunking decision",
    ).toBe(false);
  });

  it("is written in the language the code rule names", () => {
    const cyrillic = CONFIG.split("\n")
      .map((line, index) => [index + 1, line] as const)
      .filter(([, line]) => /[Ѐ-ӿ]/.test(line))
      .map(([number, line]) => `vite.config.ts:${number}: ${line.trim()}`);
    expect(
      cyrillic,
      "code and comments are English (CLAUDE.md); Russian belongs in docs/",
    ).toEqual([]);
  });
});
