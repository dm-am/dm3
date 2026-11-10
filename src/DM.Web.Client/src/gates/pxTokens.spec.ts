/**
 * @vitest-environment node
 */

/**
 * A px literal that equals a token of the same role is the token written out:
 * a font size equal to a font token of Variables.sass, a margin, padding or
 * gap equal to a step of the grid, a radius equal to a radius token, the
 * width or height of a glyph equal to the glyph token. Written out, it stops
 * following the token when the scale moves. The tokens are those of
 * Variables.sass, which vite puts before every stylesheet, so any file can
 * name them; a literal that only shares the number (a 16px icon box beside
 * the 16px tier pill) is not a match, since the role decides. A value that
 * must stay a number is in PX_REGISTER with its reason.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import {
  CLIENT_SRC,
  asPath,
  styleFiles,
} from "@/shared/lib/testing/sourceTree";
import { compileSass, ruleMap, styleTextOf } from "@/shared/lib/testing/styles";

type Role = "font" | "spacing" | "radius" | "glyph";

const GRID = new Set([
  "grid-step",
  "tiny",
  "minor",
  "small",
  "medium",
  "big",
  "major",
  "large",
]);

/** The role a token of Variables.sass plays, read from its name. */
function roleOfToken(name: string): Role | undefined {
  if (GRID.has(name)) return "spacing";
  if (name.endsWith("font-size")) return "font";
  if (name.includes("radius")) return "radius";
  if (name.includes("glyph")) return "glyph";
  return undefined;
}

/** A selector or a mixin that draws a glyph. */
const GLYPH_SELECTOR = /svg|chevron|glyph/;

/** The role a declaration plays, by its property and the rule it sits in. */
function roleOfDeclaration(
  property: string,
  selector: string,
): Role | undefined {
  if (property === "font-size") return "font";
  if (/^(margin|padding)(-|$)|^(row-|column-)?gap$/.test(property))
    return "spacing";
  if (property.startsWith("border") && property.endsWith("radius"))
    return "radius";
  if (/^(width|height)$/.test(property) && GLYPH_SELECTOR.test(selector))
    return "glyph";
  return undefined;
}

/** Every px token of Variables.sass by role and value, as the compiler resolves it. */
function tokens(): Map<Role, Map<number, string>> {
  const source = readFileSync(
    join(CLIENT_SRC, "assets/styles/Variables.sass"),
    "utf8",
  );
  const names = [...source.matchAll(/^\$([a-z-]+):/gm)]
    .map((found) => found[1])
    .filter((name) => roleOfToken(name));
  const probe = `.probe\n${names.map((name) => `  --${name}: #{$${name}}`).join("\n")}`;
  const values = ruleMap(compileSass(probe)).get(".probe") ?? {};
  const table = new Map<Role, Map<number, string>>();
  for (const name of names) {
    const px = /^(\d+(?:\.\d+)?)px$/.exec(values[`--${name}`] ?? "")?.[1];
    if (!px) continue;
    const role = roleOfToken(name) as Role;
    const byValue = table.get(role) ?? new Map<number, string>();
    // A step is named by the scale, $minor and not its source $grid-step.
    if (!byValue.has(Number(px)) || name !== "grid-step")
      byValue.set(Number(px), `$${name}`);
    table.set(role, byValue);
  }
  return table;
}

interface Use {
  selector: string;
  property: string;
  value: string;
}

/**
 * Literals that equal a token of their role and stay numbers, file by file,
 * each by the rule and property it stands in, so the same value elsewhere in
 * the file is still a finding, with why the token is not the source of the
 * value.
 */
const PX_REGISTER: Record<string, (Use & { why: string })[]> = {
  "pages/moderation/ModerationAwardsSeries.vue": [
    {
      selector: ".grant-row .grant-icon",
      property: "font-size",
      value: "20px",
      why: "sizes the award icon, drawn in em: a glyph of 20, not a line of text at the title size",
    },
  ],
  "shared/ui/SpeechBubble/SpeechBubble.vue": [
    {
      selector: ".toggle-icon",
      property: "font-size",
      value: "16px",
      why: "sizes the icon of the toggle, drawn in em: a glyph of 16, not text at the body size",
    },
  ],
};

const sameUse = (one: Use, other: Use): boolean =>
  one.selector === other.selector &&
  one.property === other.property &&
  one.value === other.value;

const DECLARATION = /^\s*([a-z-]+):\s+(.+?);?\s*$/;
const PX = /(?<![\w.$#-])(\d+(?:\.\d+)?)px\b/g;

/** Every declaration of a style text with the selector or mixin around it. */
function declarations(
  text: string,
): { property: string; value: string; selector: string }[] {
  const found: { property: string; value: string; selector: string }[] = [];
  const open: { indent: number; selector: string }[] = [];
  for (const line of text.split("\n")) {
    if (!line.trim()) continue;
    const indent = line.length - line.trimStart().length;
    while (open.length && open[open.length - 1].indent >= indent) open.pop();
    const declared = DECLARATION.exec(line);
    if (declared) {
      found.push({
        property: declared[1],
        value: declared[2].replace(/url\([^)]*\)|"[^"]*"|'[^']*'/g, ""),
        selector: open.map((one) => one.selector).join(" "),
      });
    } else {
      open.push({ indent, selector: line.trim() });
    }
  }
  return found;
}

/** The px literals of one file that a token of their role would name. */
function tokenLiterals(
  file: string,
  table: Map<Role, Map<number, string>>,
): (Use & { message: string })[] {
  const found: (Use & { message: string })[] = [];
  for (const { property, value, selector } of declarations(styleTextOf(file))) {
    const role = roleOfDeclaration(property, selector);
    if (!role) continue;
    for (const [literal, number] of value.matchAll(PX)) {
      const token = table.get(role)?.get(Number(number));
      if (token)
        found.push({
          selector,
          property,
          value: literal,
          message: `${selector} ${property}: ${value.trim()} — ${literal} is ${token}; write the token, or put the rule, property and value into PX_REGISTER with why it must not follow the token`,
        });
    }
  }
  return found;
}

describe("a px literal", () => {
  const table = tokens();

  it("reads the tokens of every role", () => {
    // A table that lost a role passes every literal of that role.
    expect([...table.keys()].sort()).toEqual([
      "font",
      "glyph",
      "radius",
      "spacing",
    ]);
    expect(table.get("spacing")?.get(8)).toBe("$small");
  });

  it("is the token when it equals a token of its role", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      for (const use of tokenLiterals(file, table)) {
        if (PX_REGISTER[path]?.some((listed) => sameUse(listed, use))) continue;
        offenders.push(`${path}: ${use.message}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it("keeps no reason for a literal that is gone", () => {
    const stale: string[] = [];
    for (const [path, listed] of Object.entries(PX_REGISTER)) {
      const found = tokenLiterals(join(CLIENT_SRC, path), table);
      for (const use of listed)
        if (!found.some((one) => sameUse(one, use)))
          stale.push(`${path}: ${use.selector} ${use.property}: ${use.value}`);
    }
    expect(stale).toEqual([]);
  });
});
