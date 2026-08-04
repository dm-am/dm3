/**
 * @vitest-environment node
 */

/**
 * A number printed next to a noun agrees with it: "1 игра", "2 игры", "5 игр".
 * Six counters spelled one form and kept it for every value, so the account
 * security history opened with "Показаны последние 3 событий" — a screen a
 * person opens exactly when something alarmed them.
 *
 * The rule is checked at the seam where it breaks: an interpolation whose
 * expression ends in a count is not followed by a Russian word. The noun after
 * a count comes out of pluralize, and a call is an interpolation of its own, so
 * the correct form leaves no bare word here to find.
 *
 * Templates are read with HTML comments stripped and styles left out, so a
 * comment or a sass rule is never a failure.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** An expression that ends in a count; an offset like `count - 3` still is one. */
// "number" and "left" are deliberately absent: roomNumber is an ordinal
// ("Комната №3 не открыта") and arrowLeft is a glyph.
const COUNT_TAIL =
  /(?:count|length|points|days|total|size|amount|quantity|remaining)\s*(?:[-+]\s*\d+\s*)?$/i;

/**
 * A count the file gave a name of its own.
 *
 * Reading only the names above is a rule about vocabulary, not about counting,
 * and the vocabulary loses: chat.ts spelled "и еще 4 оценили это" through
 * `const rest = count - 3`, and the same sentence written with a name off the
 * list was invisible. So every local binding whose initialiser is a count is a
 * count as well — one hop, which is what a line of copy tends to be away from
 * the number it prints.
 */
const NAMED_LOCALLY =
  /(?:^|\n)\s*(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*([^;\n]+)/g;

/** An initialiser that produces a number. */
function isCount(expression: string): boolean {
  const text = expression.trim().replace(/\.value$/, "");
  return (
    COUNT_TAIL.test(text) ||
    /\.(length|size)\b/.test(text) ||
    /\bMath\.\w+\(/.test(text) ||
    /^\d+$/.test(text)
  );
}

/** The counts a single file names, `.value` unwrapped. */
function countsNamedIn(parts: string[]): Set<string> {
  const names = new Set<string>();
  // Twice: a name can be defined out of another one defined below it in a
  // template-first read, and one hop of chaining is cheap.
  for (let pass = 0; pass < 2; pass++) {
    for (const part of parts) {
      NAMED_LOCALLY.lastIndex = 0;
      for (
        let hit = NAMED_LOCALLY.exec(part);
        hit;
        hit = NAMED_LOCALLY.exec(part)
      ) {
        const [, name, initialiser] = hit;
        if (isCount(initialiser) || names.has(initialiser.trim())) {
          names.add(name);
        }
      }
    }
  }
  return names;
}

/**
 * A run of interpolations and then a Russian word; a line wrap counts as the
 * space. A run rather than one, because the count is not always the last of
 * them: the editor's counter reads `${charCount.value}${limit} символов`, where
 * the nearest mustache holds " / 500" or nothing at all, and reading only that
 * one let the counter say "1 символов" on every screen that sets no limit.
 */
const INTERPOLATED = /((?:\{\{[^{}]*\}\}[ \t]*)+)\s*([А-Яа-я]{2,})(\.?)/g;

/** The same seam inside a template literal. */
const SUBSTITUTED = /((?:\$\{[^{}]*\}[ \t]*)+)([А-Яа-я]{2,})(\.?)/g;

/** The expressions of one such run, innermost braces stripped. */
function expressionsOf(run: string): string[] {
  return [...run.matchAll(/\{\{?([^{}]*)\}\}?/g)].map((hit) => hit[1].trim());
}

/**
 * A word that does not agree with the count. An impersonal short participle is
 * the same for one and for seven ("+1 запланировано", "+7 запланировано"), so
 * there is no plural here to get wrong.
 */
const INVARIANT: Record<string, string> = {
  запланировано: "impersonal short participle, the same for every count",
};

/** The rule itself lives here and nowhere else. */
const PLURAL_RULE_OWNER = "shared/lib/utils/pluralize.ts";

/** Two moderation labels held their own copy of it, keyed on the same moduli. */
const PLURAL_RULE = /%\s*100\b/;

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

function where(file: string): string {
  return relative(CLIENT_SRC, file).split("\\").join("/");
}

/** Template (HTML comments stripped) and script bodies; styles are left out. */
function readable(file: string, raw: string): string[] {
  if (file.endsWith(".ts")) return [raw];
  const { descriptor } = parseSfc(raw, { filename: file });
  const template = (descriptor.template?.content ?? "").replace(
    /<!--[\s\S]*?-->/g,
    " ",
  );
  const scripts = [descriptor.script, descriptor.scriptSetup]
    .map((block) => block?.content)
    .filter((content): content is string => typeof content === "string");
  return [template, ...scripts];
}

describe("a counted noun agrees with its count", () => {
  it("never leaves a bare noun after a count", () => {
    const offenders: string[] = [];

    for (const file of collectFiles(CLIENT_SRC)) {
      const raw = readFileSync(file, "utf8");
      if (!/[А-Яа-я]/.test(raw)) continue;

      const parts = readable(file, raw);
      const local = countsNamedIn(parts);

      for (const part of parts) {
        for (const pattern of [INTERPOLATED, SUBSTITUTED]) {
          pattern.lastIndex = 0;
          for (let hit = pattern.exec(part); hit; hit = pattern.exec(part)) {
            const [, run, word, abbreviated] = hit;
            const counted = expressionsOf(run).find((expression) => {
              const printed = expression.replace(/\.value$/, "");
              return COUNT_TAIL.test(printed) || local.has(printed);
            });
            if (counted === undefined) continue;
            // "5 б." is an abbreviation, and an abbreviation has one form.
            if (abbreviated === ".") continue;
            if (word in INVARIANT) continue;
            offenders.push(
              `${where(file)}: "${word}" is spelled for one value of ${counted}`,
            );
          }
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("keeps one implementation of the plural rule", () => {
    const offenders = collectFiles(CLIENT_SRC)
      .filter((file) => where(file) !== PLURAL_RULE_OWNER)
      .filter((file) =>
        readable(file, readFileSync(file, "utf8")).some((part) =>
          PLURAL_RULE.test(part),
        ),
      )
      .map((file) => `${where(file)}: a second copy of the plural rule`);

    expect(offenders).toEqual([]);
  });
});
