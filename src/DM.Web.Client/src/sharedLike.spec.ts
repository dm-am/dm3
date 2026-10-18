/**
 * @vitest-environment node
 */

/**
 * A like is one drawing on this site: shared/ui/LikeBadge, the heart of
 * icons.ts with the count in a plate, both in the colour of the line around
 * them. The owner's rule has two halves and both are general: no red for a
 * like anywhere, the chat included, and a heart that never changes colour,
 * hovered or not.
 *
 * Before the badge every place drew its own like - the comment, the topic card
 * and the chat message each had a heart, a count and a red of their own - and
 * the rule could only be kept by visiting all of them. So the gate asks three
 * things of the sources:
 *
 *   - the two hearts are drawn by the badge and by nobody else (ALLOWED names
 *     the few that are not a like counter, and why);
 *   - the likers' tooltip (getLikesTooltip) is built by the badge alone, which
 *     is how a place that shows likers without the badge is found even if it
 *     draws its heart some other way;
 *   - no style rule of a like names $accent-red, and none of them changes
 *     paint under the pointer or the press.
 *
 * A like's style rule is found by name: every place that draws one names its
 * classes like, reaction or heart, so a compiled selector with one of the three
 * in it belongs to a like. The message toolbar is the one exception, because
 * its liked state is the generic `.toolbar-btn.active`; its partial is compiled
 * and that state read by path. Its hover (brighten and grow) is the toolbar's
 * motion for every icon and not a colour of the like, so only red is asked of
 * it there.
 *
 * PENDING is the migration still to be done: places that draw a like of their
 * own. Each one moves onto the badge and its line goes. An entry that no longer
 * breaks any rule fails too, so the list only shrinks.
 */
import { beforeAll, describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import {
  compileSass,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const SOURCES = [".vue", ".ts", ".sass"];

/** The badge itself. */
const BADGE = "shared/ui/LikeBadge/LikeBadge.vue";

/**
 * Places that draw a heart and are not a like counter. The badge cannot stand
 * in for them: the toolbar's control is a <button> among icon buttons, and the
 * badge, a button itself, cannot nest in it.
 */
const ALLOWED: Record<string, string> = {
  [BADGE]: "the badge",
  "widgets/chat-feed/ChatFeed.vue":
    "the like of the message hover toolbar, an icon button among icon buttons",
};

/** Places that still draw a like of their own, each to move onto the badge. */
const PENDING: Record<string, string> = {};

/** A heart named in code: `name="heartEmpty"`, `'heartFilled'`. */
const HEART = /["']heart(?:Empty|Filled)["']/;

/** The likers' tooltip, imported under any local name. */
const LIKERS = /import\s*(?:type\s*)?\{[^}]*\bgetLikesTooltip\b[^}]*\}\s*from/;

/** A selector that belongs to a like. */
const LIKE_SELECTOR = /like|reaction|heart/i;

/** Declarations that change what a like looks like. */
const PAINT = new Set([
  "color",
  "fill",
  "stroke",
  "filter",
  "opacity",
  "background",
  "background-color",
  "background-image",
  "border-color",
]);

const RED = /accent-red/;

const collect = (dir: string, out: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (
      SOURCES.some((ext) => name.endsWith(ext)) &&
      !name.endsWith(".spec.ts")
    ) {
      out.push(full);
    }
  }
  return out;
};

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

/** Source lines that are code, not commentary about code. */
const codeLines = (source: string): string[] =>
  source.split("\n").filter((line) => !/^\s*(\/\/|\/\*|\*|<!--)/.test(line));

/** What a .vue file's style rules do to a like. */
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
      if (/:hover|:active/.test(selector) && PAINT.has(property)) {
        found.push(`repaints on hover/press: ${selector} { ${property} }`);
      }
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
    const code = codeLines(source);
    if (!(path in ALLOWED)) {
      add(
        path,
        code
          .filter((line) => HEART.test(line))
          .map((l) => `heart: ${l.trim()}`),
      );
    }
    if (path !== BADGE && LIKERS.test(source)) {
      add(path, ["builds the likers' tooltip itself"]);
    }
    if (file.endsWith(".vue")) add(path, styleViolations(file));
  }
  add("assets/styles/_MessageToolbar.sass", toolbarViolations());
  return found;
}

describe("every like is the shared badge", () => {
  const files = collect(CLIENT_SRC);
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

  it("draws no like of its own outside the badge", () => {
    const offenders = [...found]
      .filter(([path]) => !(path in PENDING))
      .flatMap(([path, what]) => what.map((one) => `${path}: ${one}`));
    expect(offenders).toEqual([]);
  });

  it("keeps the migration list to places that still need it", () => {
    const done = Object.keys(PENDING).filter((path) => !found.has(path));
    expect(done, "moved onto the badge: remove from PENDING").toEqual([]);
  });

  it("keeps the exceptions to places that still draw a heart", () => {
    const stale = Object.keys(ALLOWED).filter((path) => {
      const source = readFileSync(join(CLIENT_SRC, path), "utf8");
      return !codeLines(source).some((line) => HEART.test(line));
    });
    expect(stale, "no heart there any more: remove from ALLOWED").toEqual([]);
  });

  it("finds the badge itself clean", () => {
    // The badge is exempt from the heart rule only; red and hover apply to it
    // like to any like, and it must be seen by the style scan at all.
    expect(found.get(BADGE)).toBeUndefined();
    expect(
      styleRules(compileSass(sassBlocks(join(CLIENT_SRC, BADGE)))).some(
        ({ selector }) => LIKE_SELECTOR.test(selector),
      ),
    ).toBe(true);
  });
});
