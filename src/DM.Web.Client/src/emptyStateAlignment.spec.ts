/**
 * @vitest-environment node
 */

/**
 * Empty states and login prompts read from the left edge, like every other
 * paragraph on the site. "Войдите, чтобы создать игру" used to sit in the
 * middle of the page while the sentence under it started at the margin,
 * and the same phrase appeared both ways: "Оцененных постов пока нет" is
 * left-aligned on the home page and was centered in the game section.
 *
 * The centering lived in two shared components (LoginPrompt, EmptyState)
 * and in thirty local rules that each re-invented it, so the rule cannot
 * be kept by one component and has to be checked over the tree.
 *
 * The shared empty state has a second half to it, decided with the first
 * and checked at the bottom of this file: it draws no icon. It used to
 * take one and hang it at 64px above the sentence, which spent the height
 * of three lines reporting what "Нет активных сессий" already says. An
 * icon is not an alignment, so the walk above cannot see it, and an `icon`
 * left at a call site is not an error either — an undeclared prop lands on
 * the root element as a stray attribute and nothing complains.
 *
 * What counts as an empty state is decided by the class name: the words
 * below are the vocabulary the client already uses for "there is nothing
 * here" and "you are not signed in". A rule outside that vocabulary is
 * none of this test's business — a centered table header or a centered
 * calendar cell is layout, not prose.
 *
 * Horizontal centering only. For a flex block that means `justify-content`
 * in a row and `align-items` in a column; the other axis is vertical
 * placement inside the box and is left alone. `text-align: center` is
 * horizontal in every context, so it is always a violation.
 *
 * Indented Sass only, which is every style block in the client but one
 * (shared/ui/Tooltip/TooltipContent.vue ships plain CSS and holds no
 * centering at all).
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

/**
 * Class-name words that mark a block as an empty state or a sign-in
 * prompt. Matched against whole dash-separated parts of a class, so
 * `.feed-empty`, `.comments-none` and `.globalChat-event-hint` are in
 * while `.paging` and `.chain-progress` are not.
 */
const EMPTY_STATE_WORDS = [
  "empty",
  "prompt",
  "hint",
  "none",
  "not-found",
  "denied",
  "error",
  "loading",
  "state",
  "private",
  "placeholder",
];

/**
 * Blocks that match the vocabulary and still center, with the reason.
 *
 * The four loading blocks center a spinner: the graphic sits in the middle
 * of its own box, and the caption under it inherits nothing from that.
 * The activation card is one of the centered auth cards the owner has not
 * ruled on yet; it goes left the day he does, and this entry goes with it.
 */
const CENTERED_ON_PURPOSE: Record<string, string> = {
  "pages/account/AccountActivationPage.vue .resend-prompt":
    "inside the centered activation card, which awaits its own decision",
  "pages/game/GameFirstUnreadComment.vue .loading-state": "centers a spinner",
  "pages/game/GameFirstUnreadPost.vue .loading-state": "centers a spinner",
  "pages/messenger/ChatsList.vue .search-loading": "centers a spinner",
  "pages/messenger/DirectChatRedirect.vue .loading-container":
    "centers a spinner",
};

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

type Declaration = { prop: string; value: string; line: number };
type Block = { selector: string; chain: string[]; decls: Declaration[] };

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (name.endsWith(".vue") || name.endsWith(".sass")) {
      out.push(full);
    }
  }
  return out;
}

/** Every indented-Sass block in a file, with its ancestor selectors. */
function parseSass(lines: string[], firstLine: number): Block[] {
  const blocks: Block[] = [];
  const stack: { indent: number; selector: string; decls: Declaration[] }[] =
    [];
  let group: string[] = [];
  let groupIndent = 0;

  lines.forEach((raw, index) => {
    const text = raw.trim();
    if (!text || /^(\/\/|\/\*|\*)/.test(text)) return;
    const indent = raw.length - raw.trimStart().length;
    while (stack.length && stack[stack.length - 1].indent >= indent)
      stack.pop();

    // `prop: value` always has whitespace after the colon in Sass, which is
    // what separates it from `&:hover`, `a:hover` and `:global(...)`.
    const declaration = /^([a-z-]+):[ \t]+(\S.*)$/.exec(text);
    if (declaration && !text.endsWith(",")) {
      stack[stack.length - 1]?.decls.push({
        prop: declaration[1],
        value: declaration[2].trim(),
        line: firstLine + index,
      });
      return;
    }

    // A selector group spans lines: every line but the last ends in a comma.
    if (text.endsWith(",")) {
      if (!group.length) groupIndent = indent;
      group.push(text.slice(0, -1).trim());
      return;
    }
    const selector =
      group.length && groupIndent === indent
        ? [...group, text].join(", ")
        : text;
    group = [];

    const node = { indent, selector, decls: [] as Declaration[] };
    stack.push(node);
    blocks.push({
      selector,
      chain: stack.map((s) => s.selector),
      decls: node.decls,
    });
  });

  return blocks;
}

/** Indented-Sass sources of a file: the file itself, or its style blocks. */
function sassSources(file: string): { lines: string[]; firstLine: number }[] {
  const source = readFileSync(file, "utf8");
  if (file.endsWith(".sass"))
    return [{ lines: source.split("\n"), firstLine: 1 }];
  const { descriptor } = parseSfc(source, { filename: file });
  return descriptor.styles
    .filter((style) => style.lang === "sass")
    .map((style) => ({
      lines: style.content.split("\n"),
      firstLine: style.loc.start.line,
    }));
}

const WORD_PATTERNS = EMPTY_STATE_WORDS.map(
  (word) => new RegExp(`(^|-)${word}(-|$)`),
);

const isEmptyState = (chain: string[]): boolean =>
  chain.some((selector) =>
    (selector.match(/\.[A-Za-z][\w-]*/g) ?? [])
      .map((cls) => cls.slice(1).toLowerCase())
      .some((cls) => WORD_PATTERNS.some((pattern) => pattern.test(cls))),
  );

/** Declarations that push a block's own content off the left edge. */
function centeringIn(block: Block): Declaration[] {
  const declared = (prop: string, value: string) =>
    block.decls.find((d) => d.prop === prop && d.value === value);

  const found: Declaration[] = [];
  const textAlign = declared("text-align", "center");
  if (textAlign) found.push(textAlign);

  const display = block.decls.find((d) => d.prop === "display");
  if (display && /^(inline-)?flex$/.test(display.value)) {
    const direction = block.decls.find((d) => d.prop === "flex-direction");
    const horizontal =
      direction && /^column/.test(direction.value)
        ? "align-items"
        : "justify-content";
    const centered = declared(horizontal, "center");
    if (centered) found.push(centered);
  }
  return found;
}

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

function offendersIn(file: string): string[] {
  const path = asPath(file);
  const offenders: string[] = [];
  for (const { lines, firstLine } of sassSources(file)) {
    for (const block of parseSass(lines, firstLine)) {
      if (!isEmptyState(block.chain)) continue;
      if (`${path} ${block.selector}` in CENTERED_ON_PURPOSE) continue;
      for (const decl of centeringIn(block)) {
        offenders.push(
          `${path}:${decl.line} ${block.chain.join(" ")} — ${decl.prop}: ${decl.value}`,
        );
      }
    }
  }
  return offenders;
}

describe("empty states and login prompts", () => {
  const files = collect(CLIENT_SRC);

  it("reads the style sources it is supposed to read", () => {
    expect(files.length).toBeGreaterThan(200);
  });

  it("start at the left edge", () => {
    expect(files.flatMap(offendersIn)).toEqual([]);
  });

  it("keeps every deliberate exception centered, so the list cannot rot", () => {
    const stillCentered = new Set<string>();
    for (const file of files) {
      const path = asPath(file);
      for (const { lines, firstLine } of sassSources(file)) {
        for (const block of parseSass(lines, firstLine)) {
          if (!isEmptyState(block.chain)) continue;
          if (centeringIn(block).length) {
            stillCentered.add(`${path} ${block.selector}`);
          }
        }
      }
    }
    for (const [key, reason] of Object.entries(CENTERED_ON_PURPOSE)) {
      expect(stillCentered.has(key), `${key}: ${reason}`).toBe(true);
    }
  });
});

/**
 * Every `<EmptyState …>` opening tag of a file, attributes included. A fresh
 * regex per call: a `/g` one carries `lastIndex` between calls and would skip
 * the second file it is asked about.
 */
const emptyStateTags = (source: string): string[] =>
  source.match(/<EmptyState\b[^>]*>/g) ?? [];

describe("the shared empty state", () => {
  it("draws no icon", () => {
    const source = readFileSync(
      join(CLIENT_SRC, "shared/ui/EmptyState/EmptyState.vue"),
      "utf8",
    );
    expect(source).not.toContain("SvgIcon");
    expect(source).not.toContain("empty-icon");
    expect(source).not.toMatch(/^\s*icon\??:/m);
  });

  it("is handed no icon by any caller", () => {
    const offenders: string[] = [];
    for (const file of collect(CLIENT_SRC).filter((f) => f.endsWith(".vue"))) {
      for (const tag of emptyStateTags(readFileSync(file, "utf8"))) {
        if (/\bicon\b/.test(tag)) offenders.push(`${asPath(file)}: ${tag}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it("really reaches the call sites, so the check above can go red", () => {
    // Without this the walk could pass by finding no EmptyState at all.
    const callers = collect(CLIENT_SRC)
      .filter((f) => f.endsWith(".vue"))
      .filter((f) => emptyStateTags(readFileSync(f, "utf8")).length > 0);
    expect(callers.length).toBeGreaterThan(3);
  });
});
