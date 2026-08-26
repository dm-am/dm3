/**
 * @vitest-environment node
 */

/**
 * The rules about interface copy. Each can only be broken in the source, so
 * each is checked by reading the source.
 *
 * 1. UI_STANDARDS forbids the middle dot as a separator in interface copy:
 *    navigation strips use " | ", and a qualifier attached to a value goes in
 *    parentheses — the dice chip in GameRoom reads "2d6 +1 (взрыв 2)
 *    (скрытый)", both qualifiers in the same form. The rule was manual, and
 *    the one violation it had was found by reading the code rather than by a
 *    check.
 * 2. A wording the owner replaced by hand does not come back (RETIRED_COPY),
 *    and where his edit added text instead of swapping it, the text stays
 *    (REQUIRED_COPY).
 * 3. A rating with no value prints VALUE_UNAVAILABLE — never a dash, which in
 *    a column of numbers reads as a zero, and never a second hand-written copy
 *    of the token, which is why the token itself is written in one file only.
 * 4. CODE_STYLE spells the letter at U+0451 without its dots and reserves the
 *    typographic quotes for the motto. Both rules were manual, and the
 *    convention said so in as many words.
 * 5. The forum entity is a "топик". A viewer created a "топик", opened its
 *    edit form and edited a "тема", then deleted a "тема". The word "тема"
 *    means other things on the site (the colour theme, the subject of a
 *    ticket), so this one is checked where the forum lives rather than
 *    everywhere.
 * 6. The em dash is not forbidden — Russian writes an omitted copula with one,
 *    and a range needs a sign between its ends — but it is spent against a
 *    budget: a file, a count, and the argument for that count. A dash in a
 *    hint, a toast or a page title has no line to be spent on.
 *
 * This test is deliberately narrow:
 *   - only .vue and .ts under the client source tree — not docs, not styles;
 *   - in .ts files and in <script> blocks only string and template literals are
 *     read, through the TypeScript AST, so the character inside a code comment
 *     or an identifier is not a failure;
 *   - in <template> HTML comments are stripped first, for the same reason.
 *
 * A middle dot outside a literal is not copy and is not this test's business.
 * Inside one it is copy until proven otherwise, and the tree holds none.
 */
import { describe, it, expect, vi } from "vitest";

// A whole-tree AST scan legitimately outruns the 5s default when the suite
// saturates every core, which is what a coverage run does.
vi.setConfig({ testTimeout: 30_000 });
import { readFileSync, readdirSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { NOTHING_TO_SHOW, VALUE_UNAVAILABLE } from "./constants/copy";

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

/**
 * Where the missing-value token may be written out: the constant that declares
 * it, and nowhere else. Every screen that prints it imports it, the header
 * statistics block included.
 */
const TOKEN_ALLOWED = new Set(["shared/lib/constants/copy.ts"]);

/**
 * The token as a word rather than as a whole string. Comparing a part against
 * the token caught it only in a script literal that held nothing else: a
 * template prints it inside an expression or as one text node among many
 * ({{ row.description || "n/a" }}), and there the part compared is the whole
 * template, which is never equal to three characters. That is the shape the
 * nineteen hand-written copies had, so the check was blind to exactly the thing
 * it was written against.
 *
 * The neighbours excluded are letters, digits and the slash: "moderation/awards"
 * holds these three characters between two letters and is a path, not copy.
 */
const TOKEN_SPELLED = new RegExp(
  `(^|[^\\p{L}\\p{N}/])${VALUE_UNAVAILABLE.replace(/[.*+?^${}()|[\]\\/]/g, "\\$&")}([^\\p{L}\\p{N}/]|$)`,
  "u",
);

/**
 * The empty-table wording, under the same rule and for the same reason.
 *
 * The finding was not "one string is written twice" but "one idea is worded
 * five ways", and a check that knows only about the n/a token cannot say that.
 * It was blind to the copy it was raised over: the IP list of a moderated
 * profile printed the empty-table sentence by hand, right next to the
 * DataTable default spelling the very same one.
 */
const EMPTY_SPELLED = new RegExp(
  `(^|[^\\p{L}])${NOTHING_TO_SHOW}([^\\p{L}]|$)`,
  "u",
);

/** "тема" as a whole word: other words merely start with it. */
const TEMA = /(?<![А-я])[Тт]ем(?:а|ы|е|у|ой|ам|ах|ами)(?![А-я])/g;

/**
 * The context decides, not the path.
 *
 * The rule read "check the files whose path names the forum", which is a rule
 * about where the word was last caught rather than about the word. It left out
 * the dictionary of notification headings
 * (entities/notification/lib/notificationTitle.ts): the line a reader sees over
 * every forum notification sits in a file named after neither a topic nor a
 * forum, so "Новая тема на форуме" would have passed it. Six more files name the
 * entity from outside those directories -- the router, the home page, the news
 * block, the testimonials page, the profile and the rules.
 *
 * So the whole client is read, and the word is flagged where the forum stands
 * next to it. Next to it, and not in the same string: a .vue template is one
 * blob of text and every forum page mentions the forum somewhere in it.
 */
const FORUM_NEARBY = /форум|топик|topic/i;

/** How much text around the word counts as its neighbourhood, in characters. */
const NEIGHBOURHOOD = 60;

/**
 * The senses the word keeps. Each is the subject of something -- a
 * conversation, a letter, a ticket -- or the colour scheme, and none of them is
 * the entity. They are listed because they do stand next to the forum: the
 * rules page writes "уход от темы в служебных разделах форума", which is about
 * staying on subject and not about a "топик".
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

const HERE = dirname(fileURLToPath(import.meta.url));
// lib -> shared -> src
const CLIENT_SRC = resolve(HERE, "..", "..");

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

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

/**
 * The walk reads the whole source tree, and eight assertions below want it. One
 * walk per assertion made this file the slowest in the suite and pushed it past
 * even a raised timeout on a busy machine; the tree cannot change mid-run, so
 * the walk happens once and every assertion reads the same list.
 */
let treeCache: string[] | undefined;

function sourceFiles(): string[] {
  treeCache ??= collectFiles(CLIENT_SRC);
  return treeCache;
}

/** Path as a failure message spells it: relative, forward slashes. */
function where(file: string): string {
  return relative(CLIENT_SRC, file).split("\\").join("/");
}

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

/**
 * Every user-visible string of a file, whitespace runs collapsed inside each
 * one. A phrase then survives a re-wrap by the formatter, while two strings
 * that merely sit next to each other still cannot spell one between them.
 */
/**
 * Parsing is the expensive half: every .ts goes through the TypeScript AST and
 * every .vue through the SFC compiler. Eight assertions ask the same files for
 * the same copy, so the parse happens once per file and the result is reused.
 * Keyed by path, which is identity enough here: the tree is read-only for the
 * length of the run.
 */
const copyCache = new Map<string, string[]>();

/** Source of a file, read once, for the same reason. */
const rawCache = new Map<string, string>();

function rawOf(file: string): string {
  let raw = rawCache.get(file);
  if (raw === undefined) {
    raw = readFileSync(file, "utf8");
    rawCache.set(file, raw);
  }
  return raw;
}

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
 * Wordings the owner replaced by hand. None of these is a style rule the code
 * could re-derive: each is a page name, a sentence he dictated, or a promise
 * the site does not keep, so a list is what keeps them fixed.
 */
const RETIRED_COPY: { text: string; instead: string }[] = [
  {
    text: "в топ-десятках",
    instead: '"в топах вебсайта" — the statistics page is not ten rows deep',
  },
  {
    text: "форму обращения",
    instead:
      '"форму поддержки" for /support, "форму жалоб" for /complaint — the site has exactly those two form names',
  },
  {
    text: "форма ошибок",
    instead: '"форма поддержки" — /support takes more than bug reports',
  },
  {
    text: "ответим на этот адрес",
    instead:
      'the truth while no ticket mail exists: the answer appears in "Мои обращения", and for a guest on the tracking page',
  },
  {
    text: "вышлем ссылку для отслеживания обращения",
    instead:
      "the number and the link are printed on the success card, nothing is mailed",
  },
  {
    text: "мы ответим по нему",
    instead:
      "where the answer appears: on the site, not through the contact the author left",
  },
  {
    text: "А с полной статистикой сайта",
    instead: 'one sentence: "..., а с полной статистикой сайта ..."',
  },
  {
    text: "Ментор",
    instead:
      '"Наставник" — the badge, the roles table, the notepad hint and the user filter all say so',
  },
  {
    // The comparison is case-sensitive, and a sentence about the role names it
    // in the middle: "назначить ментора" is the same retired word.
    text: "ментор",
    instead: '"наставник" — the role has one name in every case',
  },
  {
    text: "Переход к теме",
    instead: '"Переход к топику" — the forum entity has one name',
  },
  {
    text: "арт конкурс",
    instead: '"арт-конкурс" with the hyphen Russian puts there',
  },
  {
    text: "Арт конкурс",
    instead: '"Арт-конкурс" with the hyphen Russian puts there',
  },
  {
    text: 'с тегом "без мата"',
    instead:
      'the tag as it is named: "Без мата" — a reader searches the filter for the form the rules quoted',
  },
  {
    text: "Загрузить еще",
    instead: '"Показать еще" — one action, one word for it',
  },
  {
    text: "Пожалуйста, войдите снова",
    instead:
      'the direct form its neighbours use: "Сессия истекла. Войдите снова."',
  },
  {
    text: "тысячи игроков создают",
    instead:
      '"тысячи участников" — "игрок" is a role inside a game, and the masters are not one',
  },
];

/**
 * The longest space-free run of a phrase. A wrap only ever falls on
 * whitespace, so a file whose raw text lacks this cannot hold the phrase and
 * does not have to be parsed.
 */
function probeOf(phrase: string): string {
  return phrase.split(" ").reduce((a, b) => (b.length > a.length ? b : a));
}

/**
 * The other half of the same edits. Dropping the support link out of the
 * hacking rule, splitting the news line back in two, or leaving a guest the
 * tracking link without the number it carries, brings no retired wording back,
 * so the scan above cannot see it. Each anchor is the shortest phrase that
 * carries the point, so re-wording around it is still free.
 */
const REQUIRED_COPY: { file: string; what: string; pattern: RegExp }[] = [
  {
    file: "pages/rules/RulesPage.vue",
    what: "the hacking rule sends a vulnerability report to the support form",
    pattern:
      /сначала согласуйте с администрацией через\s*<router-link to="\/support"\s*>\s*<strong>форму поддержки<\/strong>/,
  },
  {
    file: "pages/home/RecentNews.vue",
    what: "the news line stays one sentence",
    pattern: /, а с полной статистикой сайта/,
  },
  {
    file: "features/support-ticket/ui/SupportTicketForm.vue",
    what: "a guest reads the ticket number itself, not only a link holding it",
    pattern: /Номер обращения:\s*<code>\{\{ trackingToken \}\}/,
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

/**
 * Where the em dash stays, and how many of it. Nothing here is a matter of
 * taste: every entry is either grammar or a separator between two values.
 */
const EM_DASH_BUDGET: { file: string; count: number }[] = [
  // A dictionary of BBCode tags: lemma, separator, gloss.
  { file: "shared/ui/BBCodeEditor/BBCodeEditor.vue", count: 18 },
  // Filter chips: the dash sits between the two ends of a range. The date
  // ranges all draw through formatDateRangeForDisplay, so the separator is
  // spelled once there; what is left in UsersFilter are the numeric ranges.
  { file: "features/user-filter/ui/UsersFilter.vue", count: 4 },
  { file: "shared/lib/filters/utils.ts", count: 1 },
  // Credits: a role, the omitted copula, the name that holds it.
  { file: "widgets/footer/Footer.vue", count: 4 },
  // Definitions in the legal pages and in the rules. Russian writes the
  // omitted copula between two noun phrases as a dash, so these are grammar.
  { file: "pages/rules/RulesBans.vue", count: 11 },
  { file: "pages/legal/PrivacyPolicyPage.vue", count: 10 },
  { file: "pages/legal/UserAgreementPage.vue", count: 8 },
  { file: "pages/rules/RulesAuthors.vue", count: 3 },
  { file: "pages/rules/RulesExternalLinks.vue", count: 3 },
  { file: "pages/rules/RulesPage.vue", count: 3 },
  { file: "pages/rules/RulesIntro.vue", count: 1 },
  // The copula dash of the About lead.
  { file: "pages/about/AboutPage.vue", count: 1 },
  // The brand lockup in the h1, and the invitation under the testimonials:
  // "поделитесь и своим — в топике на форуме", released by the owner by name.
  { file: "pages/home/HomePage.vue", count: 2 },
];

/**
 * The sum of the budget: 18 glosses, 5 range separators, 4 credit lines,
 * 41 definitions and one invitation the owner released by name. Spelled out so the number stays a claim someone argued for
 * rather than whatever the tree happens to hold today.
 */
const EM_DASH_TOTAL = 69;

/**
 * What draws as a colour pictograph rather than as text.
 *
 * Unicode's own property and not a hand-written table of ranges. The blocks are
 * mixed: U+2713 and U+2717 are the check and the cross the symbol registry
 * uses, U+2605 is the rating star, U+26A0 is the warning sign — all text by
 * default — and they sit among the characters that are not. A range covering
 * the block forbids the site's own monochrome symbols and reads, wrongly, like
 * a rule somebody verified.
 *
 * The variation selector is the second half: any text-default character
 * followed by U+FE0F is asked to draw in colour, which is the same defect
 * written differently.
 */
const EMOJI_PRESENTATION = /\p{Emoji_Presentation}|\uFE0F/u;

describe("interface copy", () => {
  it("never uses the middle dot in a user-visible string", () => {
    const offenders = sourceFiles().flatMap((file) =>
      offendersIn(file, MIDDOT),
    );
    expect(offenders).toEqual([]);
  });

  it("never brings back a wording the owner replaced", () => {
    const files = sourceFiles();
    const offenders: string[] = [];

    for (const { text, instead } of RETIRED_COPY) {
      const probe = probeOf(text);
      for (const file of files) {
        const raw = rawOf(file);
        if (!raw.includes(probe)) continue;
        if (copyOf(file, raw).some((part) => part.includes(text))) {
          offenders.push(`${where(file)}: "${text}" — use ${instead}`);
        }
      }
    }
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
