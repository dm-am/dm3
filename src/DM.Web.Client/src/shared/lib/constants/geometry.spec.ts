/**
 * @vitest-environment node
 */

/**
 * Two scales, one of them in a language the other cannot read.
 *
 * Every distance on the site comes from Variables.sass, except the ones a
 * popover computes: those are arithmetic and arithmetic is script. So the
 * numbers were copied across, and the copies were annotated instead of linked —
 * `const gap = 2; // matches $tiny offset of other dropdowns`. That comment is
 * true until somebody edits `$grid-step`, at which point every dropdown on the
 * site moves and the calendar stays, and nothing anywhere reports it.
 *
 * geometry.ts holds the script side. This file is the link between the two: it
 * reads the Sass and fails when they disagree, which is the whole guarantee the
 * comment was pretending to give.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import { GRID_STEP, TINY, SMALL } from "./geometry";

const HERE = dirname(fileURLToPath(import.meta.url));
// shared/lib/constants -> src
const CLIENT_SRC = resolve(HERE, "..", "..", "..");
const VARIABLES = readFileSync(
  join(CLIENT_SRC, "assets/styles/Variables.sass"),
  "utf8",
);

/**
 * A step of the Sass scale, in pixels. The scale is written as multiples of
 * `$grid-step` (`$small: 2 * $grid-step`) with one half step spelled as a
 * division (`$tiny: calc($grid-step / 2)`), so both forms are resolved here
 * rather than matched literally: a rewrite of one declaration into the other
 * is not a change of value and must not fail this file.
 */
function sassStep(name: string): number {
  const declaration = new RegExp(`^\\$${name}:\\s*(.+)$`, "m").exec(VARIABLES);
  expect(declaration, `Variables.sass declares no $${name}`).not.toBeNull();
  const value = (declaration as RegExpExecArray)[1].trim();

  const px = /^(\d+(?:\.\d+)?)px$/.exec(value);
  if (px) return Number(px[1]);

  const multiple = /^(\d+(?:\.\d+)?)\s*\*\s*\$grid-step$/.exec(value);
  if (multiple) return Number(multiple[1]) * sassStep("grid-step");

  const half = /^calc\(\$grid-step\s*\/\s*(\d+(?:\.\d+)?)\)$/.exec(value);
  if (half) return sassStep("grid-step") / Number(half[1]);

  throw new Error(
    `$${name} is declared as "${value}", a shape this check cannot resolve — teach it the form or write the step as a multiple of $grid-step`,
  );
}

describe("the spacing scale JavaScript positions popovers with", () => {
  it("reads the stylesheet it is supposed to read", () => {
    // A regex that matched nothing would make every comparison below vacuous.
    expect(VARIABLES).toContain("$grid-step:");
    expect(sassStep("grid-step")).toBeGreaterThan(0);
  });

  it("is the same scale the stylesheets use", () => {
    expect({
      "grid-step": GRID_STEP,
      tiny: TINY,
      small: SMALL,
    }).toEqual({
      "grid-step": sassStep("grid-step"),
      tiny: sassStep("tiny"),
      small: sassStep("small"),
    });
  });
});
