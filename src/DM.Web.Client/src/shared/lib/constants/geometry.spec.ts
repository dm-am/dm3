/**
 * @vitest-environment node
 */

// The script side of the spacing scale against Variables.sass.
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import { GRID_STEP, TINY, SMALL, MEDIUM } from "./geometry";

const HERE = dirname(fileURLToPath(import.meta.url));
// shared/lib/constants -> src
const CLIENT_SRC = resolve(HERE, "..", "..", "..");
const VARIABLES = readFileSync(
  join(CLIENT_SRC, "assets/styles/Variables.sass"),
  "utf8",
);

/**
 * A step of the Sass scale in pixels, from either form (`2 * $grid-step` or
 * `calc($grid-step / 2)`), so rewriting one into the other does not fail.
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
      medium: MEDIUM,
    }).toEqual({
      "grid-step": sassStep("grid-step"),
      tiny: sassStep("tiny"),
      small: sassStep("small"),
      medium: sassStep("medium"),
    });
  });
});
