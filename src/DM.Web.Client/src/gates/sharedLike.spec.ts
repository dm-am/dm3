/**
 * @vitest-environment node
 */

/**
 * A like is one control on this site: shared/ui/LikeBadge, the word "Нравится"
 * with the count in brackets, in the colour of the line around it; no heart is
 * drawn for it, and no red anywhere, the chat included. The gate asks three
 * things of the sources: no heart icon is named in code; the likers' tooltip
 * (getLikesTooltip) is built by the badge and by the like of the message
 * toolbar, the one like that is not the badge, a word among icon buttons; and
 * no style rule of a like names $accent-red.
 *
 * A like's style rule is found by name (like, reaction or heart in a compiled
 * selector). The message toolbar's liked state is the generic
 * `.toolbar-btn.active`, read by path from its partial.
 */
import { beforeAll, describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import {
  compileSass,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

const SOURCES = [".vue", ".ts", ".sass"];

/** The badge itself. */
const BADGE = "shared/ui/LikeBadge/LikeBadge.vue";

/**
 * Where the likers' tooltip is built. The toolbar's like is a <button> among
 * icon buttons, and the badge, a button itself, cannot nest in it.
 */
const LIKERS_BUILT: Record<string, string> = {
  [BADGE]: "the badge",
  "widgets/chat-feed/MessageToolbar.vue":
    "the like of the message hover toolbar, a word among icon buttons",
};

/** A heart named in code: `name="heartEmpty"`, `'heartFilled'`. */
const HEART = /["']heart(?:Empty|Filled)["']/;

/** The likers' tooltip, imported under any local name. */
const LIKERS = /import\s*(?:type\s*)?\{[^}]*\bgetLikesTooltip\b[^}]*\}\s*from/;

/** A selector that belongs to a like. */
const LIKE_SELECTOR = /like|reaction|heart/i;

const RED = /accent-red/;

const isScanned = (name: string): boolean =>
  SOURCES.some((ext) => name.endsWith(ext)) && !name.endsWith(".spec.ts");

/** Source lines that are code, not commentary about code. */
const codeLines = (source: string): string[] =>
  source.split("\n").filter((line) => !/^\s*(\/\/|\/\*|\*|<!--)/.test(line));

/** The red a .vue file's style rules give a like. */
function styleViolations(file: string): string[] {
  const source = readFileSync(file, "utf8");
  if (!/<style[^>]*lang="sass"/.test(source)) return [];
  // Compiling is the slow part: a sheet with no selector line naming a like
  // is skipped. A declaration, a comment, an @-rule and a mixin include are
  // not selector lines.
  const selectors = sassBlocks(file)
    .split("\n")
    .filter((line) => !/^\s*(\/\/|@|\+|[\w-]+:\s)/.test(line));
  if (!selectors.some((line) => LIKE_SELECTOR.test(line))) return [];
  const found: string[] = [];
  for (const { selector, declarations } of styleRules(
    compileSass(sassBlocks(file), file),
  )) {
    if (!LIKE_SELECTOR.test(selector)) continue;
    for (const [property, value] of Object.entries(declarations)) {
      if (RED.test(value)) found.push(`red: ${selector} { ${property} }`);
    }
  }
  return found;
}

/** The liked state of the message toolbar, read out of its partial. */
function toolbarViolations(): string[] {
  const css = compileSass(
    '@use "@/assets/styles/MessageToolbar" as *\n+message-toolbar',
  );
  const liked = styleRules(css).filter(({ selector }) =>
    selector.includes(".toolbar-btn.active"),
  );
  if (liked.length === 0) {
    throw new Error("the message toolbar has no .toolbar-btn.active any more");
  }
  return liked.flatMap(({ selector, declarations }) =>
    Object.entries(declarations)
      .filter(([, value]) => RED.test(value))
      .map(([property]) => `red: ${selector} { ${property} }`),
  );
}

type Violations = Map<string, string[]>;

function violations(files: string[]): Violations {
  const found: Violations = new Map();
  const add = (path: string, what: string[]) => {
    if (what.length) found.set(path, [...(found.get(path) ?? []), ...what]);
  };
  for (const file of files) {
    const path = asPath(file);
    const source = readFileSync(file, "utf8");
    add(
      path,
      codeLines(source)
        .filter((line) => HEART.test(line))
        .map((l) => `heart: ${l.trim()}`),
    );
    if (!(path in LIKERS_BUILT) && LIKERS.test(source)) {
      add(path, ["builds the likers' tooltip itself"]);
    }
    if (file.endsWith(".vue")) add(path, styleViolations(file));
  }
  add("assets/styles/_MessageToolbar.sass", toolbarViolations());
  return found;
}

describe("every like is the shared badge", () => {
  const files = collect(CLIENT_SRC, isScanned);
  let found: Violations = new Map();

  // Compiling the sheets that name a like takes seconds on a busy machine,
  // which is collection time the test timeout does not cover.
  beforeAll(() => {
    found = violations(files);
  }, 60_000);

  it("reads the sources it is supposed to read", () => {
    // A walk that found nothing would pass every assertion below.
    expect(files.length).toBeGreaterThan(400);
    expect(files.map(asPath)).toContain(BADGE);
  });

  it("draws no heart and keeps no red for a like", () => {
    const offenders = [...found].flatMap(([path, what]) =>
      what.map((one) => `${path}: ${one}`),
    );
    expect(offenders).toEqual([]);
  });

  it("builds the likers' tooltip only where the list says", () => {
    const stale = Object.keys(LIKERS_BUILT).filter(
      (path) => !LIKERS.test(readFileSync(join(CLIENT_SRC, path), "utf8")),
    );
    expect(stale, "no tooltip built there any more: remove it").toEqual([]);
  });

  it("finds the badge's own style rules", () => {
    // The badge must be seen by the style scan at all.
    expect(
      styleRules(compileSass(sassBlocks(join(CLIENT_SRC, BADGE)))).some(
        ({ selector }) => LIKE_SELECTOR.test(selector),
      ),
    ).toBe(true);
  });
});
