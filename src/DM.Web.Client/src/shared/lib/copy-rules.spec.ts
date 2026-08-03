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
 *    of the token, which is why the token itself is written in two files only.
 * 4. CODE_STYLE spells the letter at U+0451 without its dots and reserves the
 *    typographic quotes for the motto. Both rules were manual, and the
 *    convention said so in as many words.
 * 5. The forum entity is a "топик". A viewer created a топик, opened its edit
 *    form and edited a тема, then deleted a тема. The word "тема" means other
 *    things on the site (the colour theme, the subject of a ticket), so this
 *    one is checked where the forum lives rather than everywhere.
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
import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { VALUE_UNAVAILABLE } from "./constants/copy";

/** U+00B7 MIDDLE DOT, spelled by code point so this file stays clean itself. */
const MIDDOT = "\u00B7";

/** U+2014 EM DASH, spelled the same way and for the same reason. */
const EM_DASH = "\u2014";

/**
 * The BBCode help dialog is a dictionary: a tag on the left, what it does on
 * the right, and between them the separator a dictionary has always used. It is
 * the one file where a dash may be the whole of a text node.
 */
/** U+0451 and U+0401, by code point so this file stays clean of them itself. */
const E_WITH_DOTS = ["\u0451", "\u0401"];

/** U+00AB and U+00BB, the quotes CODE_STYLE reserves for one place. */
const GUILLEMETS = ["\u00AB", "\u00BB"];

/** That place, and the one file where the pair is not a quote at all. */
const GUILLEMETS_ALLOWED: Record<string, string> = {
  "pages/about/AboutPage.vue": "the motto, the exception the convention names",
  "pages/dev/StyleVariantsPage.vue":
    "prev/next arrows of a calendar in a development-only mockup",
};

/**
 * Where the missing-value token may be written out. The constant declares it;
 * the header statistics block prints it for a whole row of numbers that failed
 * to load, which is not the "no value" case the constant is named for.
 */
const TOKEN_ALLOWED = new Set([
  "shared/lib/constants/copy.ts",
  "widgets/header/SiteStatistics.vue",
]);

/** Where the forum entity is named, and therefore where its name is checked. */
const FORUM_SURFACE = ["pages/forum", "features/topic", "entities/forum"];

/** "\u0442\u0435\u043C\u0430" as a whole word: other words merely start with it. */
const TEMA =
  /(?<![\u0410-\u044F])[\u0422\u0442]\u0435\u043C(?:\u0430|\u044B|\u0435|\u0443|\u043E\u0439|\u0430\u043C|\u0430\u0445|\u0430\u043C\u0438)(?![\u0410-\u044F])/;

const GLOSSARY = "shared/ui/BBCodeEditor/BBCodeEditor.vue";

/** The module that declares the token is the one place that may spell it. */
const TOKEN_HOME = "shared/lib/constants/copy.ts";

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
  const raw = readFileSync(file, "utf8");
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
function copyOf(file: string, raw: string): string[] {
  const parts = file.endsWith(".ts")
    ? stringLiterals(raw, file)
    : (() => {
        const { template, scripts } = sfcParts(raw, file);
        return [template, ...scripts.flatMap((s) => stringLiterals(s, file))];
      })();
  return parts.map((part) => part.replace(/\s+/g, " "));
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

/**
 * Complete string literals only. A template literal's fragments are not whole
 * strings, so the range label `${min} — ${max}` a filter chip builds stays a
 * range instead of reading as a missing value.
 */
function wholeLiterals(code: string, fileName: string): string[] {
  const source = ts.createSourceFile(
    fileName,
    code,
    ts.ScriptTarget.Latest,
    true,
  );
  const found: string[] = [];
  const visit = (node: ts.Node): void => {
    if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) {
      found.push(node.text);
    }
    ts.forEachChild(node, visit);
  };
  ts.forEachChild(source, visit);
  return found;
}

/** Script bodies: the whole file for a .ts, the blocks of an SFC. */
function scriptsOf(file: string, raw: string): string[] {
  return file.endsWith(".ts") ? [raw] : sfcParts(raw, file).scripts;
}

/** Text between tags. An attribute value lives inside a tag and is not text. */
function textNodes(template: string): string[] {
  return template.split(/<[^>]*>/);
}

/**
 * Every way a file can spell a token out itself: as the whole of a text node,
 * as a quoted literal inside an interpolation or a binding, as a complete
 * literal in script. `home` is the module allowed to declare it.
 */
function spelledOut(
  file: string,
  raw: string,
  token: string,
  home: string,
): string[] {
  const rel = where(file);
  if (rel === home) return [];

  const hits: string[] = [];
  for (const script of scriptsOf(file, raw)) {
    if (wholeLiterals(script, file).some((text) => text.trim() === token)) {
      hits.push(`${rel}: a literal that is only "${token}"`);
    }
  }
  if (!file.endsWith(".vue")) return hits;

  const { template } = sfcParts(raw, file);
  if (textNodes(template).some((text) => text.trim() === token)) {
    hits.push(`${rel}: markup printing "${token}" and nothing else`);
  }
  const quoted = token.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  if (new RegExp(`["']\\s*${quoted}\\s*["']`).test(template)) {
    hits.push(`${rel}: "${token}" written into an expression`);
  }
  return hits;
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
  { file: "shared/ui/BBCodeEditor/BBCodeEditor.vue", count: 19 },
  // Filter chips: the dash sits between the two ends of a range.
  { file: "features/user-filter/ui/UsersFilter.vue", count: 5 },
  { file: "features/game-filter/ui/GamesFilter.vue", count: 4 },
  { file: "features/blog-filter/ui/BlogsFilter.vue", count: 3 },
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
  // The brand lockup, the one index.html also carries in og:title.
  { file: "pages/about/AboutPage.vue", count: 1 },
  { file: "pages/home/HomePage.vue", count: 1 },
];

/**
 * The sum of the budget: 19 glosses, 13 range separators, 4 credit lines and
 * 41 definitions. Spelled out so the number stays a claim someone argued for
 * rather than whatever the tree happens to hold today.
 */
const EM_DASH_TOTAL = 77;

describe("interface copy", () => {
  it("never uses the middle dot in a user-visible string", () => {
    const offenders = collectFiles(CLIENT_SRC).flatMap((file) =>
      offendersIn(file, MIDDOT),
    );
    expect(offenders).toEqual([]);
  });

  it("never brings back a wording the owner replaced", () => {
    const files = collectFiles(CLIENT_SRC);
    const offenders: string[] = [];

    for (const { text, instead } of RETIRED_COPY) {
      const probe = probeOf(text);
      for (const file of files) {
        const raw = readFileSync(file, "utf8");
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

  it("never spells the letter with the two dots", () => {
    const offenders = collectFiles(CLIENT_SRC).flatMap((file) =>
      E_WITH_DOTS.flatMap((letter) => offendersIn(file, letter)),
    );
    expect(offenders).toEqual([]);
  });

  it("keeps the typographic quotes at the motto", () => {
    const offenders = collectFiles(CLIENT_SRC)
      .filter((file) => !(where(file) in GUILLEMETS_ALLOWED))
      .flatMap((file) => GUILLEMETS.flatMap((mark) => offendersIn(file, mark)));
    expect(offenders).toEqual([]);
  });

  it("calls the forum entity a топик", () => {
    const offenders: string[] = [];
    for (const file of collectFiles(CLIENT_SRC)) {
      const rel = where(file);
      if (!FORUM_SURFACE.some((dir) => rel.startsWith(`${dir}/`))) continue;
      const raw = readFileSync(file, "utf8");
      if (copyOf(file, raw).some((part) => TEMA.test(part))) {
        offenders.push(`${rel}: the forum entity is a "топик"`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it("writes the missing-value token in one place", () => {
    const offenders: string[] = [];
    for (const file of collectFiles(CLIENT_SRC)) {
      const rel = where(file);
      if (TOKEN_ALLOWED.has(rel)) continue;
      const raw = readFileSync(file, "utf8");
      if (!raw.includes(VALUE_UNAVAILABLE)) continue;
      if (copyOf(file, raw).some((part) => part === VALUE_UNAVAILABLE)) {
        offenders.push(`${rel}: spells the token instead of importing it`);
      }
    }
    expect(offenders).toEqual([]);
  });
});
