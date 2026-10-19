/**
 * @vitest-environment node
 */

/**
 * A number printed next to a noun agrees with it: "1 игра", "2 игры", "5 игр".
 * The rule is checked at the seam where it breaks: an interpolation whose
 * expression ends in a count is not followed by a Russian word, since the noun
 * after a count comes out of pluralize, an interpolation of its own. Templates
 * are read with HTML comments stripped and styles left out.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** An expression that ends in a count; an offset like `count - 3` still is one. */
// "number" and "left" are deliberately absent: roomNumber is an ordinal
// ("Комната №3 не открыта") and arrowLeft is a glyph.
const COUNT_TAIL =
  /(?:count|length|points|days|total|size|amount|quantity|remaining)\s*(?:[-+]\s*\d+\s*)?$/i;

/**
 * A count the file gave a name of its own (`const rest = count - 3`): every
 * local binding whose initialiser is a count is a count as well, one hop away.
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
 * space. A run, because the count is not always the last of them:
 * `${charCount.value}${limit} символов`.
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

/** A plural rule of its own, keyed on the same moduli. */
const PLURAL_RULE = /%\s*100\b/;

/**
 * The words after an interpolation the rule reads, at the least, in the
 * components and in the modules: a reading that lost either kind of file
 * would empty half the corpus and keep passing.
 */
const READ_AT_LEAST = { ".vue": 3, ".ts": 10 };

const collectFiles = (): string[] =>
  collect(
    CLIENT_SRC,
    (name) =>
      (name.endsWith(".ts") || name.endsWith(".vue")) &&
      !name.endsWith(".spec.ts") &&
      !name.endsWith(".d.ts"),
  );

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
  // A walk that came back short, or a reading of the files that found no text
  // in them, would pass both rules below without a word.
  it("reads the sources it is about, the owner of the plural rule among them", () => {
    const files = collectFiles().map(asPath);
    expect(files.length).toBeGreaterThan(500);
    expect(files).toContain(PLURAL_RULE_OWNER);

    const owner = join(CLIENT_SRC, PLURAL_RULE_OWNER);
    const parts = readable(owner, readFileSync(owner, "utf8"));
    expect(parts.some((part) => PLURAL_RULE.test(part))).toBe(true);
  });

  it("never leaves a bare noun after a count", () => {
    const offenders: string[] = [];
    // Every word read after an interpolation, counted or not: the corpus the
    // rule is applied to, which a broken reading would empty.
    const read = { ".vue": 0, ".ts": 0 };

    for (const file of collectFiles()) {
      const raw = readFileSync(file, "utf8");
      if (!/[А-Яа-я]/.test(raw)) continue;

      const parts = readable(file, raw);
      const local = countsNamedIn(parts);

      for (const part of parts) {
        for (const pattern of [INTERPOLATED, SUBSTITUTED]) {
          pattern.lastIndex = 0;
          for (let hit = pattern.exec(part); hit; hit = pattern.exec(part)) {
            const [, run, word, abbreviated] = hit;
            read[file.endsWith(".vue") ? ".vue" : ".ts"]++;
            const counted = expressionsOf(run).find((expression) => {
              const printed = expression.replace(/\.value$/, "");
              return COUNT_TAIL.test(printed) || local.has(printed);
            });
            if (counted === undefined) continue;
            // "5 б." is an abbreviation, and an abbreviation has one form.
            if (abbreviated === ".") continue;
            if (word in INVARIANT) continue;
            offenders.push(
              `${asPath(file)}: "${word}" is spelled for one value of ${counted}`,
            );
          }
        }
      }
    }

    expect(offenders).toEqual([]);
    expect(read[".vue"]).toBeGreaterThan(READ_AT_LEAST[".vue"]);
    expect(read[".ts"]).toBeGreaterThan(READ_AT_LEAST[".ts"]);
  });

  it("keeps one implementation of the plural rule", () => {
    const offenders = collectFiles()
      .filter((file) => asPath(file) !== PLURAL_RULE_OWNER)
      .filter((file) =>
        readable(file, readFileSync(file, "utf8")).some((part) =>
          PLURAL_RULE.test(part),
        ),
      )
      .map((file) => `${asPath(file)}: a second copy of the plural rule`);

    expect(offenders).toEqual([]);
  });
});
