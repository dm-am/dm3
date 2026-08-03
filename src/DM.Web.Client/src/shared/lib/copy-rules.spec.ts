/**
 * @vitest-environment node
 */

/**
 * Three rules about interface copy. Each can only be broken in the source, so
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
 * 3. A rating with no value prints RATING_UNAVAILABLE — never a dash, which in
 *    a column of numbers reads as a zero, and never a second hand-written copy
 *    of the token.
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
import { RATING_UNAVAILABLE } from "./constants/user";

/** U+00B7 MIDDLE DOT, spelled by code point so this file stays clean itself. */
const MIDDOT = "\u00B7";

/** U+2014 EM DASH, spelled the same way and for the same reason. */
const EM_DASH = "\u2014";

/**
 * A name about a rating: the word itself, or the tail of a camelCase name such
 * as authorRating. Case-sensitive on purpose, because generatingCode carries
 * the same letters and none of the meaning.
 */
const RATING_NAME = /\brating|Rating/;

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

/** Where a rating is rendered, and therefore where the token belongs. */
function ratingContexts(template: string, file: string): string[] {
  const out: string[] = [];
  // A component that IS the rating: all of its template.
  if (/Rating\.vue$/.test(file)) out.push(template);
  // A table's rating cell. The cell nests templates of its own, so the block
  // runs to the next cell slot rather than to the next closing tag.
  const slot = /<template\s+#cell-rating\b/g;
  for (let m = slot.exec(template); m !== null; m = slot.exec(template)) {
    const next = template.indexOf("<template #cell-", m.index + 1);
    out.push(template.slice(m.index, next === -1 ? undefined : next));
  }
  // Anything interpolated out of a rating.
  for (const [expr] of template.matchAll(/\{\{[^}]*\}\}/g)) {
    if (RATING_NAME.test(expr)) out.push(expr);
  }
  return out;
}

/**
 * Placeholder literals inside a declaration named about a rating. Only a
 * literal that IS the placeholder counts: the users filter builds a range
 * label `${ratingMin} — ${ratingMax}`, where the dash is punctuation between
 * two numbers rather than a missing value.
 */
function ratingPlaceholders(code: string, fileName: string): string[] {
  const source = ts.createSourceFile(
    fileName,
    code,
    ts.ScriptTarget.Latest,
    true,
  );
  const found: string[] = [];
  const visit = (node: ts.Node, inRating: boolean): void => {
    const named =
      (ts.isVariableDeclaration(node) ||
        ts.isFunctionDeclaration(node) ||
        ts.isMethodDeclaration(node) ||
        ts.isPropertyAssignment(node)) &&
      node.name !== undefined &&
      RATING_NAME.test(node.name.getText(source));
    const scope = inRating || named;
    const text =
      ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)
        ? node.text
        : null;
    if (scope && (text === EM_DASH || text === RATING_UNAVAILABLE)) {
      found.push(text);
    }
    ts.forEachChild(node, (child) => visit(child, scope));
  };
  ts.forEachChild(source, (node) => visit(node, false));
  return found;
}

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

  it("prints RATING_UNAVAILABLE where a rating has no value", () => {
    // The single token every site below is measured against.
    expect(RATING_UNAVAILABLE).toBe("n/a");

    const offenders: string[] = [];
    for (const file of collectFiles(CLIENT_SRC)) {
      const raw = readFileSync(file, "utf8");
      if (!RATING_NAME.test(raw)) continue;
      if (!raw.includes(EM_DASH) && !raw.includes(RATING_UNAVAILABLE)) continue;

      const { template, scripts } = file.endsWith(".vue")
        ? sfcParts(raw, file)
        : { template: "", scripts: [raw] };

      for (const script of scripts) {
        for (const hit of ratingPlaceholders(script, file)) {
          offenders.push(`${where(file)}: rating code spells "${hit}" itself`);
        }
      }
      for (const context of ratingContexts(template, file)) {
        if (context.includes(EM_DASH)) {
          offenders.push(`${where(file)}: dash in a rating position`);
        }
        if (context.includes(RATING_UNAVAILABLE)) {
          offenders.push(
            `${where(file)}: "${RATING_UNAVAILABLE}" written out instead of RATING_UNAVAILABLE`,
          );
        }
      }
    }
    expect(offenders).toEqual([]);
  });
});
