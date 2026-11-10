/**
 * @vitest-environment node
 */

/**
 * The rules about the form of interface copy, read in the string literals of
 * .ts and <script> (TypeScript AST) and in <template> without HTML comments:
 * no middle dot (UI_STANDARDS), the missing-value token and the empty-table
 * wording written once, the letter at U+0451 without dots and the typographic
 * quotes at the motto only (CODE_STYLE), the forum entity a "топик", the em
 * dash spent against a budget, and two sentences the owner dictated kept.
 */
import { describe, it, expect, vi } from "vitest";

// A whole-tree AST scan outruns the 5s default on a saturated machine.
vi.setConfig({ testTimeout: 60_000 });
import { readFileSync } from "fs";
import { join } from "path";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { NOTHING_TO_SHOW, VALUE_UNAVAILABLE } from "./constants/copy";
import { CLIENT_SRC, asPath, collect } from "./testing/sourceTree";

/** U+00B7 MIDDLE DOT, spelled by code point so this file stays clean itself. */
const MIDDOT = "\u00B7";

/** U+2014 EM DASH, spelled the same way and for the same reason. */
const EM_DASH = "\u2014";

/** U+0451 and U+0401, by code point so this file stays clean of them itself. */
const E_WITH_DOTS = ["\u0451", "\u0401"];

/** U+00AB and U+00BB, the quotes CODE_STYLE reserves for one place. */
const GUILLEMETS = ["\u00AB", "\u00BB"];

/** That place, and only it. */
const GUILLEMETS_ALLOWED: Record<string, string> = {
  "pages/about/AboutPage.vue": "the motto, the exception the convention names",
};

/** Where the missing-value token may be written out: its constant only. */
const TOKEN_ALLOWED = new Set(["shared/lib/constants/copy.ts"]);

/**
 * The token as a word, inside a template expression or a text node too. The
 * neighbours excluded are letters, digits and the slash: "moderation/awards"
 * is a path, not copy.
 */
const TOKEN_SPELLED = new RegExp(
  `(^|[^\\p{L}\\p{N}/])${VALUE_UNAVAILABLE.replace(/[.*+?^${}()|[\]\\/]/g, "\\$&")}([^\\p{L}\\p{N}/]|$)`,
  "u",
);

/** The empty-table wording, under the same rule. */
const EMPTY_SPELLED = new RegExp(
  `(^|[^\\p{L}])${NOTHING_TO_SHOW}([^\\p{L}]|$)`,
  "u",
);

/** "тема" as a whole word: other words merely start with it. */
const TEMA = /(?<![А-я])[Тт]ем(?:а|ы|е|у|ой|ам|ах|ами)(?![А-я])/g;

/**
 * The context decides, not the path: the word is flagged where the forum stands
 * near it, not in the same string, since a template is one blob of text.
 */
const FORUM_NEARBY = /форум|топик|topic/i;

/** How much text around the word counts as its neighbourhood, in characters. */
const NEIGHBOURHOOD = 60;

/**
 * The senses the word keeps next to the forum: the subject of a conversation,
 * a letter or a ticket, and the colour scheme.
 */
const OTHER_SENSES: RegExp[] = [
  // The subject at hand: "уход от темы", "не по теме", "на эту тему".
  /(уход от|не по|по|на эту|об этой) тем[аыеу]/i,
  // A subject line: of a letter, of a complaint, of a support ticket.
  /тем[аыеу] (письма|жалобы|обращения)/i,
  // The colour theme.
  /(темн|светл)\w* тем|тем[аыеу] (оформления|сайта)|(переключени|настро)\w* темы|между темами/i,
];

/** The forum entity called by the other word, with the text around each hit. */
function callsTheEntityATema(part: string): boolean {
  TEMA.lastIndex = 0;
  for (const hit of part.matchAll(TEMA)) {
    const at = hit.index ?? 0;
    const around = part.slice(
      Math.max(0, at - NEIGHBOURHOOD),
      at + hit[0].length + NEIGHBOURHOOD,
    );
    if (!FORUM_NEARBY.test(around)) continue;
    if (OTHER_SENSES.some((sense) => sense.test(around))) continue;
    return true;
  }
  return false;
}

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

// Walked once: every assertion below reads the same tree.
let treeCache: string[] | undefined;

function sourceFiles(): string[] {
  treeCache ??= collect(CLIENT_SRC, isSource);
  return treeCache;
}

const where = asPath;

/** Text of every string and template literal; comments and names excluded. */
function stringLiterals(code: string, fileName: string): string[] {
  const source = ts.createSourceFile(
    fileName,
    code,
    ts.ScriptTarget.Latest,
    true,
  );
  const found: string[] = [];
  const visit = (node: ts.Node): void => {
    if (
      ts.isStringLiteral(node) ||
      ts.isNoSubstitutionTemplateLiteral(node) ||
      ts.isTemplateHead(node) ||
      ts.isTemplateMiddle(node) ||
      ts.isTemplateTail(node)
    ) {
      found.push(node.text);
    }
    ts.forEachChild(node, visit);
  };
  ts.forEachChild(source, visit);
  return found;
}

/** Template (HTML comments stripped) and raw <script> bodies of one SFC. */
function sfcParts(
  raw: string,
  file: string,
): {
  template: string;
  scripts: string[];
} {
  const { descriptor } = parseSfc(raw, { filename: file });
  return {
    template: (descriptor.template?.content ?? "").replace(
      /<!--[\s\S]*?-->/g,
      "",
    ),
    scripts: [descriptor.script, descriptor.scriptSetup]
      .map((block) => block?.content)
      .filter((content): content is string => typeof content === "string"),
  };
}

function offendersIn(file: string, needle: string): string[] {
  const raw = rawOf(file);
  // Cheap reject: almost no file contains the needle at all.
  if (!raw.includes(needle)) return [];

  const hits: string[] = [];

  if (file.endsWith(".ts")) {
    if (stringLiterals(raw, file).some((text) => text.includes(needle))) {
      hits.push(`${where(file)} (string literal)`);
    }
    return hits;
  }

  const { template, scripts } = sfcParts(raw, file);
  if (template.includes(needle)) hits.push(`${where(file)} (template text)`);

  for (const script of scripts) {
    if (stringLiterals(script, file).some((t) => t.includes(needle))) {
      hits.push(`${where(file)} (script literal)`);
    }
  }
  return hits;
}

// Parsed once per file: every assertion asks the same files for the same copy.
const copyCache = new Map<string, string[]>();

/** Source of a file, read once. */
const rawCache = new Map<string, string>();

function rawOf(file: string): string {
  let raw = rawCache.get(file);
  if (raw === undefined) {
    raw = readFileSync(file, "utf8");
    rawCache.set(file, raw);
  }
  return raw;
}

/** Every user-visible string of a file, whitespace runs collapsed in each. */
function copyOf(file: string, raw: string): string[] {
  const cached = copyCache.get(file);
  if (cached) return cached;

  const parts = file.endsWith(".ts")
    ? stringLiterals(raw, file)
    : (() => {
        const { template, scripts } = sfcParts(raw, file);
        return [template, ...scripts.flatMap((s) => stringLiterals(s, file))];
      })();
  const copy = parts.map((part) => part.replace(/\s+/g, " "));
  copyCache.set(file, copy);
  return copy;
}

/**
 * Text the owner's edits added rather than swapped, which no scan for a wording
 * sees go. Each anchor is the shortest phrase that carries the point.
 */
const REQUIRED_COPY: { file: string; what: string; pattern: RegExp }[] = [
  {
    file: "pages/rules/RulesPage.vue",
    what: "the hacking rule sends a vulnerability report to the support form",
    pattern:
      /сначала согласуйте с администрацией через\s*<router-link :to="supportFor\('Bug'\)"\s*>\s*<strong>форму поддержки<\/strong>/,
  },
  {
    file: "pages/home/RecentNews.vue",
    what: "the news line stays one sentence",
    pattern: /, а с полной статистикой сайта/,
  },
];

/** Script bodies: the whole file for a .ts, the blocks of an SFC. */
function scriptsOf(file: string, raw: string): string[] {
  return file.endsWith(".ts") ? [raw] : sfcParts(raw, file).scripts;
}

/** Every em dash a reader of one file can see. */
function emDashCount(file: string, raw: string): number {
  const dashes = (text: string): number => text.split(EM_DASH).length - 1;
  const inScripts = scriptsOf(file, raw)
    .flatMap((script) => stringLiterals(script, file))
    .reduce((total, text) => total + dashes(text), 0);
  if (file.endsWith(".ts")) return inScripts;
  return inScripts + dashes(sfcParts(raw, file).template);
}

/** Where the em dash stays: grammar, or a separator between two values. */
const EM_DASH_BUDGET: { file: string; count: number }[] = [
  // The separator of a date range and of a number range in filter chips.
  { file: "shared/lib/filters/values.ts", count: 2 },
  // Credits: a role, the omitted copula, the name that holds it.
  { file: "widgets/footer/Footer.vue", count: 4 },
  // Definitions in the legal pages and the rules: the omitted copula.
  { file: "pages/rules/RulesBans.vue", count: 11 },
  { file: "pages/legal/PrivacyPolicyPage.vue", count: 10 },
  { file: "pages/legal/UserAgreementPage.vue", count: 8 },
  { file: "pages/rules/RulesAuthors.vue", count: 3 },
  { file: "pages/rules/RulesExternalLinks.vue", count: 3 },
  { file: "pages/rules/RulesPage.vue", count: 3 },
  { file: "pages/rules/RulesIntro.vue", count: 1 },
  // The copula dash of the About lead.
  { file: "pages/about/AboutPage.vue", count: 1 },
  // The brand lockup in the h1, and the invitation under the testimonials.
  { file: "pages/home/HomePage.vue", count: 2 },
];

// The sum, spelled out so it stays argued for: 2 ranges, 4 credits, 41
// definitions and the invitation.
const EM_DASH_TOTAL = 48;

/**
 * What draws as a colour pictograph: Unicode's own property, since the blocks
 * mix in the site's monochrome symbols, plus the variation selector U+FE0F.
 */
const EMOJI_PRESENTATION = /\p{Emoji_Presentation}|\uFE0F/u;

describe("interface copy", () => {
  it("never uses the middle dot in a user-visible string", () => {
    const offenders = sourceFiles().flatMap((file) =>
      offendersIn(file, MIDDOT),
    );
    expect(offenders).toEqual([]);
  });

  it("keeps the sentences the owner dictated", () => {
    const missing = REQUIRED_COPY.filter(({ file, pattern }) => {
      const full = join(CLIENT_SRC, file);
      const { template } = sfcParts(readFileSync(full, "utf8"), full);
      return !pattern.test(template.replace(/\s+/g, " "));
    }).map(({ file, what }) => `${file}: ${what}`);
    expect(missing).toEqual([]);
  });

  it("draws an icon with an icon and not with an emoji", () => {
    const offenders: string[] = [];
    for (const file of sourceFiles()) {
      for (const part of copyOf(file, rawOf(file))) {
        // Iterating a string yields code points, so a surrogate pair arrives
        // whole — which is the only way the pictograph planes are reachable.
        for (const character of part) {
          if (!EMOJI_PRESENTATION.test(character)) continue;
          const point = character.codePointAt(0) as number;
          offenders.push(
            `${where(file)}: U+${point.toString(16).toUpperCase()} draws as a colour emoji, and the icon registry has forty monochrome ones`,
          );
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("never spells the letter with the two dots", () => {
    const offenders = sourceFiles().flatMap((file) =>
      E_WITH_DOTS.flatMap((letter) => offendersIn(file, letter)),
    );
    expect(offenders).toEqual([]);
  });

  it("keeps the typographic quotes at the motto", () => {
    const offenders = sourceFiles()
      .filter((file) => !(where(file) in GUILLEMETS_ALLOWED))
      .flatMap((file) => GUILLEMETS.flatMap((mark) => offendersIn(file, mark)));
    expect(offenders).toEqual([]);
  });

  it("spends the em dash only where the budget says", () => {
    const counted: Record<string, number> = {};
    for (const file of sourceFiles()) {
      const raw = rawOf(file);
      if (!raw.includes(EM_DASH)) continue;
      const count = emDashCount(file, raw);
      if (count) counted[where(file)] = count;
    }
    const budgeted = Object.fromEntries(
      EM_DASH_BUDGET.map(({ file, count }) => [file, count]),
    );

    expect(counted).toEqual(budgeted);
    expect(EM_DASH_BUDGET.reduce((sum, { count }) => sum + count, 0)).toBe(
      EM_DASH_TOTAL,
    );
  });

  it("calls the forum entity a топик", () => {
    const offenders: string[] = [];
    let scanned = 0;
    for (const file of sourceFiles()) {
      scanned++;
      const raw = rawOf(file);
      if (copyOf(file, raw).some(callsTheEntityATema)) {
        offenders.push(`${where(file)}: the forum entity is a "топик"`);
      }
    }
    expect(offenders).toEqual([]);
    // A rule that reads nothing passes.
    expect(scanned).toBeGreaterThan(300);
  });

  it.each([
    ["missing value", VALUE_UNAVAILABLE, TOKEN_SPELLED],
    ["empty table", NOTHING_TO_SHOW, EMPTY_SPELLED],
  ])("writes the %s wording in one place", (_what, token, spelled) => {
    const offenders: string[] = [];
    for (const file of sourceFiles()) {
      const rel = where(file);
      if (TOKEN_ALLOWED.has(rel)) continue;
      const raw = rawOf(file);
      if (!raw.includes(token)) continue;
      if (copyOf(file, raw).some((part) => spelled.test(part))) {
        offenders.push(`${rel}: spells the wording instead of importing it`);
      }
    }
    expect(offenders).toEqual([]);
  });
});
