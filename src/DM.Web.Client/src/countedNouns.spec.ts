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
const COUNT_TAIL =
  /(?:count|length|points|days|total|size)\s*(?:[-+]\s*\d+\s*)?$/i;

/** `{{ ... }}` and then a Russian word; a line wrap counts as the space. */
const INTERPOLATED = /\{\{([^{}]*)\}\}\s*([А-Яа-я]{2,})(\.?)/g;

/** The same seam inside a template literal. */
const SUBSTITUTED = /\$\{([^{}]*)\}[ \t]*([А-Яа-я]{2,})(\.?)/g;

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

      for (const part of readable(file, raw)) {
        for (const pattern of [INTERPOLATED, SUBSTITUTED]) {
          pattern.lastIndex = 0;
          for (let hit = pattern.exec(part); hit; hit = pattern.exec(part)) {
            const [, expression, word, abbreviated] = hit;
            if (!COUNT_TAIL.test(expression.trim())) continue;
            // "5 б." is an abbreviation, and an abbreviation has one form.
            if (abbreviated === ".") continue;
            if (word in INVARIANT) continue;
            offenders.push(
              `${where(file)}: "${word}" is spelled for one value of ${expression.trim()}`,
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
