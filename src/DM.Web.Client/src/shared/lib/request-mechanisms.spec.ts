/**
 * @vitest-environment node
 */

/**
 * One cache and one race guard in the client, counted from the tree.
 *
 * The audit found four answers to "what happens when the reader asks for the
 * same list twice" — a composable with its own TTL, a keyed cache with another
 * one, two stores with hand-written timestamps, and screens with no cache at
 * all — and two answers to "what happens when the answers come back out of
 * order": a shared guard, and hand-rolled version counters. The cost was not
 * the duplication. It was that a change to caching had to be made in four
 * places, that missing one of them broke nothing any test could see, and that
 * three separate race bugs in that same audit each landed in the copy that had
 * no guard.
 *
 * So the count itself is the rule, and it is read off the tree rather than
 * trusted: a fifth mechanism is a new file that keeps its own stored-at
 * timestamp, and nothing but a check like this notices one arriving.
 *
 * The checks are written against SHAPE, not against vocabulary. An earlier
 * version of this file looked for a closed list of identifiers — `storedAt`,
 * `lastFetch*`, `cacheMs`, `*Version`, `*RequestId` — which only ever proved that
 * a regular expression can find the words it was written from. A module spelling
 * its timestamp `loadedAt` and its window `CACHE_WINDOW_MS` walked straight past
 * it, and so did a real hand-rolled race guard already in the tree, whose counter
 * was called `pendencyGeneration`. What is looked for now is what a cache
 * unavoidably does: read the clock, keep the reading, and later subtract that same
 * reading from the clock again. Names are free.
 *
 * What the tests below do NOT claim: that every screen must cache. A profile
 * subpage that reads one endpoint while it is mounted is right not to. The
 * claim is that a screen which does cache uses the one cache, and a screen
 * which races uses the one guard.
 *
 * Nor do they claim to catch every possible hand-rolled guard. One form is out
 * of reach of a check like this: comparing the request KEY instead of a counter,
 * which is two strings compared for equality and indistinguishable by shape from
 * any other string comparison in the tree. The pulse store held the one instance
 * and was moved to the shared guard by hand; a check written for it would have
 * had to exempt a dozen honest comparisons, and a rule that is mostly exemptions
 * is a list pretending to be a rule.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// lib -> shared -> src
const CLIENT_SRC = resolve(HERE, "..", "..");

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** The one cache, and the one guard. */
const THE_CACHE = "shared/lib/utils/keyedCache.ts";
const THE_GUARD = "shared/lib/utils/requestGuard.ts";

/**
 * The composables that stand on the guard. They are named because a consumer
 * that reaches them has reached the single implementation underneath, not
 * because they are exempt from anything.
 */
const VIA_THE_GUARD = /createRequestGuard|useGuardedRequest/;

/** Regex-safe form of an identifier path like `entry.storedAt` or `x.value`. */
function literal(name: string): string {
  return name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Reading the clock, in every spelling the platform offers.
 *
 * `Date.now()` alone was what this file looked for at first, which made the
 * promise below ("names are free") false in the one place it mattered: a cache
 * written with `new Date().getTime()` — the same arithmetic, the same TTL, the
 * same consequences — passed the check without being noticed. The spelling of the
 * clock is vocabulary too.
 *
 * A bare `new Date()` is the last of them and was the last to be added: a cache
 * that keeps the Date object rather than the number (`storedAt: new Date()`,
 * `Date.now() - slot.storedAt.getTime()`) does the same three things in the same
 * order, and was still walking past this check after the other four spellings
 * were listed.
 */
const CLOCK =
  "(?:Date\\.now\\(\\)|new Date\\(\\)\\.(?:getTime|valueOf)\\(\\)|\\+new Date\\(\\)|" +
  "performance\\.now\\(\\)|new Date\\(\\))";

/**
 * Every name the module writes a clock reading into: `storedAt: Date.now()`,
 * `loadedAt = new Date().getTime()`, `formLoadTime.value = Date.now()`. The
 * left-hand side is taken whole, dots included, because that is how it will be
 * read back.
 */
function recordedMoments(code: string): string[] {
  const found = new Set<string>();
  const pattern = new RegExp(`([\\w$.[\\]]+)\\s*(?:=|:)\\s*${CLOCK}`, "g");
  let match: RegExpExecArray | null;
  while ((match = pattern.exec(code)) !== null) found.add(match[1]);
  return [...found];
}

/**
 * Does the module later measure the clock against a reading it kept?
 *
 * Both directions: an age (`Date.now() - storedAt`) and a countdown
 * (`expiresAt - Date.now()`). This pair — record a moment, then subtract it from
 * the clock — is what a cache is, whatever its identifiers are called. It is also
 * what separates one from the other clock readings in the tree: a bot link
 * counting down to an expiry the server sent, a subscriber list deciding who
 * counts as inactive, a relative-time helper. Those measure against a moment they
 * were given; this measures against one it wrote down itself.
 */
function measuresAgainstItsOwnReading(code: string): boolean {
  return recordedMoments(code).some((name) => {
    const it = literal(name);
    return (
      new RegExp(`${CLOCK}\\s*-\\s*[\\w$.[\\]"']*${it}`).test(code) ||
      new RegExp(`${it}[\\w$.]*\\s*-\\s*${CLOCK}`).test(code)
    );
  });
}

/** Asking a guard whether this answer is still the one being waited for. */
const CHECKS_THE_GUARD = /\bisCurrent\s*\(/;

/**
 * The shape a hand-rolled race guard has: a mutable counter that gets bumped, and
 * whose bump is later compared against a copy somebody kept. Take a number, start
 * a request, and on the answer ask whether the number is still yours — that is the
 * whole mechanism, and what it is named is not part of it.
 *
 * Indentation is not part of it either. This check used to require the
 * declaration at column zero, which sounded like "module level" and meant
 * something else: the three counters in the forum store that this file was
 * written to keep out stood inside `defineStore(() => { ... })`, indented by two
 * spaces, and the check never saw one of them. It was green on its own defect for
 * as long as it existed. What the anchor was really for was excluding a `for
 * (let i = 0; ...)` header, and the character before the declaration says that
 * on its own.
 *
 * The comparison has to be against another name, not against a literal: `if
 * (childIndex === 0)` inside a serializer loop is a counter compared to a
 * constant, which is arithmetic and not a race guard.
 *
 * Where the counter is kept is not part of it either. `let n = 0` was the only
 * declaration this looked for, and in a store or a component the same counter is
 * far more likely to be written `const n = ref(0)` and bumped through `n.value`
 * — a `const` holding a mutable box, which the earlier pattern could not match
 * on either half. Both spellings are read here, and what is bumped and compared
 * is the box in the second case.
 */
function handRolledCounters(code: string): string[] {
  const found: string[] = [];
  // Anything but "(" before the declaration: that excludes a for-header and
  // keeps every real declaration, at whatever depth it sits.
  const declarations: [RegExp, (name: string) => string][] = [
    [/(?:^|[;{}])[^\S\n]*(?:let|var)\s+([\w$]+)\s*=\s*0\b/gm, (name) => name],
    [
      /(?:^|[;{}])[^\S\n]*(?:const|let|var)\s+([\w$]+)\s*=\s*(?:shallowRef|ref)\s*(?:<[^>]*>)?\s*\(\s*0\s*\)/gm,
      (name) => `${name}.value`,
    ],
  ];

  for (const [declaration, accessOf] of declarations) {
    let match: RegExpExecArray | null;
    while ((match = declaration.exec(code)) !== null) {
      const access = accessOf(match[1]);
      const it = literal(access);
      const bumped = new RegExp(
        `\\+\\+${it}\\b|\\b${it}\\+\\+|\\b${it}\\s*\\+=`,
      ).test(code);
      const comparedToAnotherName =
        new RegExp(`[A-Za-z_$][\\w$]*\\s*(?:!==|===)\\s*${it}\\b`).test(code) ||
        new RegExp(`\\b${it}\\s*(?:!==|===)\\s*[A-Za-z_$]`).test(code);
      if (bumped && comparedToAnotherName) found.push(access);
    }
  }
  return found;
}

/**
 * The places that measure age against a recorded moment and are not a cache of
 * answers from the server. Written out rather than left to slip past a pattern,
 * so the boundary of this check is stated instead of accidental.
 *
 * They are here because the shape check above catches them honestly: each really
 * does record a moment and measure against it. None of them is answering "do I
 * have to ask the server again", which is the question this file is about.
 */
const NOT_A_RESPONSE_CACHE: Record<string, string> = {
  "shared/lib/utils/bbcode.ts":
    "a stopwatch around one synchronous parse, which is over before the next " +
    "statement runs and is never asked about again",
  "shared/ui/BBCodeEditor/BBCodeEditor.vue":
    "localStorage draft of unsent text, not an answer from the server",
  "features/auth/ui/LoginForm.vue":
    "how long the reader spent on the form, which is a bot signal",
  "features/auth/ui/RegistrationForm.vue":
    "how long the reader spent on the form, which is a bot signal",
};

function collectFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collectFiles(full, out);
    } else if (
      (full.endsWith(".ts") || full.endsWith(".vue")) &&
      !full.endsWith(".spec.ts") &&
      !full.endsWith(".d.ts")
    ) {
      out.push(full);
    }
  }
  return out;
}

/** Path as a failure message spells it: relative, forward slashes. */
function where(file: string): string {
  return relative(CLIENT_SRC, file).split("\\").join("/");
}

const SOURCES = collectFiles(CLIENT_SRC).map((file) => ({
  path: where(file),
  code: readFileSync(file, "utf8"),
}));

describe("caching", () => {
  it("decides freshness in exactly one place", () => {
    const implementations = SOURCES.filter(
      ({ path, code }) =>
        measuresAgainstItsOwnReading(code) && !(path in NOT_A_RESPONSE_CACHE),
    ).map(({ path }) => path);

    // Not "at most one": the list is spelled out, so deleting the cache and
    // scattering the arithmetic again fails here too.
    expect(implementations).toEqual([THE_CACHE]);
  });

  it("names every exemption it grants", () => {
    // The exemption list is the one part of the check above that is a list, so
    // it is the one part that can rot: a file renamed or deleted leaves an entry
    // that silently exempts nothing and hides that the rule has changed shape.
    const exempted = Object.keys(NOT_A_RESPONSE_CACHE);
    const present = SOURCES.filter(({ path }) => exempted.includes(path));

    expect(present.map(({ path }) => path).sort()).toEqual(
      [...exempted].sort(),
    );
    for (const { path, code } of present) {
      expect(
        measuresAgainstItsOwnReading(code),
        `${path} no longer measures against a moment it recorded — it does not ` +
          `need an exemption, and keeping one blinds the check to whatever ` +
          `arrives in that file next`,
      ).toBe(true);
    }
  });
});

describe("the race guard", () => {
  it("is declared in exactly one place", () => {
    const declarations = SOURCES.filter(({ code }) =>
      /export function createRequestGuard/.test(code),
    ).map(({ path }) => path);

    expect(declarations).toEqual([THE_GUARD]);
  });

  it("is the only thing anyone asks whether an answer is still wanted", () => {
    const strays = SOURCES.filter(
      ({ path, code }) =>
        path !== THE_GUARD &&
        CHECKS_THE_GUARD.test(code) &&
        !VIA_THE_GUARD.test(code),
    ).map(({ path }) => path);

    expect(strays).toEqual([]);
  });

  it("is not rebuilt as a counter of somebody's own", () => {
    const counters = SOURCES.filter(({ path }) => path !== THE_GUARD).flatMap(
      ({ path, code }) => handRolledCounters(code).map((n) => `${path} (${n})`),
    );

    // Three of these stood in one forum store, each bumped and compared by
    // hand, and the store also imported the shared guard for other calls. A
    // fourth stood in MentorPanel and was missed for two waves because it called
    // itself a "generation" and the check at the time knew only the word
    // "version" — hence a shape here rather than a vocabulary.
    //
    // requestGuard itself is left out because it is the counter: it is where the
    // bump and the comparison are supposed to live, and the test above pins it to
    // that one file.
    expect(counters).toEqual([]);
  });

  it("reaches a screen through the composable, not by hand", () => {
    const assembled = SOURCES.filter(
      ({ path, code }) =>
        path.endsWith(".vue") && /createRequestGuard/.test(code),
    ).map(({ path }) => path);

    // Six components each wrote out the same loading ref, error ref, guard and
    // isCurrent checks. A component holds its request; it does not hold the
    // machinery — useGuardedRequest does.
    expect(assembled).toEqual([]);
  });
});
