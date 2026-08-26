/**
 * @vitest-environment node
 */

/**
 * A moderation page a viewer may not open says so, and says it in the words of
 * one source. Twelve pages held twelve copies of the sentence and seven had no
 * copy at all: there a 403 surfaced as "Не удалось загрузить данные", which
 * names a failure that did not happen — the data would have loaded, the viewer
 * is simply not allowed.
 *
 * ModerationPage is out of the checks: it is the shell around the pages, and
 * its tab strip filters itself from the same table the pages gate themselves
 * with (lib/sections.ts). ModerationOverview is in: it renders no data, but an
 * authenticated non-moderator opening /moderation directly must still read a
 * refusal rather than an invitation to pick a section.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SENTENCE = "Страница доступна только";
const SENTENCE_OWNER = "lib/useRoleGate.ts";
const NOT_A_GATED_PAGE = new Set(["ModerationPage.vue"]);

function collectFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) collectFiles(full, out);
    else if (!full.endsWith(".spec.ts")) out.push(full);
  }
  return out;
}

function where(file: string): string {
  return relative(HERE, file).split("\\").join("/");
}

const gatedPages = readdirSync(HERE).filter(
  (name) => name.endsWith(".vue") && !NOT_A_GATED_PAGE.has(name),
);

/**
 * The file that answers for a page's gate.
 *
 * Usually the page itself. A page whose whole template is one component out of
 * this same directory — the two premoderation queues are one shared table with
 * different nouns — is gated by that component, and the checks below have to
 * follow it there. Following is not the same as excusing: the delegate is read
 * in the page's stead, so a queue that stopped opening on the refusal fails
 * for every page that leans on it.
 */
function gateSource(name: string): string {
  const source = readFileSync(join(HERE, name), "utf8");
  const opens = source
    .slice(source.indexOf("<template>"))
    .match(/^<template>\s*<([A-Z]\w+)[\s>]/);
  const delegate = opens?.[1];
  const imported =
    delegate && source.includes(`import ${delegate} from "./${delegate}.vue"`);
  return imported
    ? readFileSync(join(HERE, `${delegate}.vue`), "utf8")
    : source;
}

describe("the moderation role gate", () => {
  it("spells the refusal in one place", () => {
    const offenders = collectFiles(HERE)
      .filter((file) => where(file) !== SENTENCE_OWNER)
      .filter((file) => readFileSync(file, "utf8").includes(SENTENCE))
      .map((file) => `${where(file)}: writes the refusal itself`);

    expect(offenders).toEqual([]);
  });

  it("explains the refusal on every page that has data to refuse", () => {
    expect(gatedPages.length).toBeGreaterThan(0);

    const offenders = gatedPages.filter((name) => {
      const source = gateSource(name);
      return !source.includes("useRoleGate(") || !source.includes("deniedText");
    });

    expect(offenders).toEqual([]);
  });

  /**
   * Calling the gate is not the same as showing what it answered.
   *
   * A page that asks about the load first — `v-if="error"`, refusal in the
   * `v-else-if` — puts the sentence back where the finding found it: the fetch
   * behind the page answers 403, the error branch wins, and the reader is told
   * "Не удалось загрузить данные" about data that was never going to load.
   * Every page opens on the refusal today; the check above was blind to the
   * order and stayed green when the two branches were swapped.
   */
  it("answers the refusal before it reports a failed load", () => {
    const OPENS_ON_REFUSAL = /<[^>]*\sv-if="!hasAccess"/;
    const ASKS_ABOUT_THE_LOAD = /\sv-(?:else-)?if="[^"]*\b(error|loading)\b/g;

    const offenders = gatedPages.flatMap((name) => {
      const source = gateSource(name);
      const template = source.slice(source.indexOf("<template>"));

      const refusal = template.search(OPENS_ON_REFUSAL);
      if (refusal < 0) {
        return [`${name}: the refusal is not the branch the template opens on`];
      }

      ASKS_ABOUT_THE_LOAD.lastIndex = 0;
      const earlier = [...template.matchAll(ASKS_ABOUT_THE_LOAD)].filter(
        (hit) => (hit.index ?? 0) < refusal,
      );
      return earlier.map(
        (hit) =>
          `${name}: ${hit[0].trim()} is asked before the refusal, so a 403 comes out as a failed load`,
      );
    });

    expect(offenders).toEqual([]);
  });
});
