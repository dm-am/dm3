/**
 * @vitest-environment node
 */

/**
 * A control that takes keyboard focus shows where the focus is.
 *
 * The scale has one ring, declared twice: globally in Reset.sass for links,
 * buttons and form controls, and inside +input-base for the fields that take
 * the mixin. Nothing else needs to draw one.
 *
 * What breaks it is a component hiding the ring on `:focus`. That reads as "no
 * ring when clicked", which is what the author meant, but `:focus-visible` is a
 * subset of `:focus`, so the rule takes the keyboard ring away as well — and it
 * did, on the search field, the date field, the range picker, the poll editor,
 * the password reset form, the fundraising page, the award series page, the
 * chat composer and the notepad. Between them that is most of the typing a
 * person does on the site, and a keyboard reader had nothing to go by. The
 * remaining border-colour change measured 1.8:1 against the resting state,
 * under the 3:1 WCAG 2.4.11 asks for.
 *
 * The narrowing is `:focus:not(:focus-visible)`, which is the pattern the
 * mixin already used in one place. This rule is what keeps the other nine from
 * coming back.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const STYLE_FILES = [".vue", ".sass", ".scss", ".css"];

/**
 * Where hiding the ring on plain `:focus` is right anyway.
 *
 * The mixin declares `:focus-visible` immediately below its `:focus`, at the
 * same specificity and later in the file, so the ring it hides comes straight
 * back for the keyboard. The editor frame is `:focus-within` — it reacts to
 * focus landing inside it, and the control that took the focus keeps its own
 * ring.
 */
const ALLOWED: Record<string, string> = {
  "assets/styles/Inputs.sass":
    "the mixin restores it on :focus-visible two lines below",
  "shared/ui/BBCodeEditor/BBCodeEditor.vue":
    ":focus-within on the frame, not on a control",
};

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (STYLE_FILES.some((ext) => name.endsWith(ext))) {
      out.push(full);
    }
  }
  return out;
}

const where = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

/** The selector a declaration sits under: the nearest line indented less. */
function selectorAbove(lines: string[], index: number): string | null {
  const indent = lines[index].match(/^\s*/)![0].length;
  for (let i = index - 1; i >= 0 && i > index - 12; i--) {
    if (!lines[i].trim()) continue;
    if (lines[i].match(/^\s*/)![0].length >= indent) continue;
    return lines[i].trim();
  }
  return null;
}

describe("the focus ring", () => {
  const offenders: string[] = [];
  const files = collect(CLIENT_SRC);

  for (const file of files) {
    const path = where(file);
    if (path in ALLOWED) continue;
    const lines = readFileSync(file, "utf8").split("\n");
    lines.forEach((line, index) => {
      if (!/^\s*outline:\s*none/.test(line)) return;
      const selector = selectorAbove(lines, index);
      if (!selector || !/:focus/.test(selector)) return;
      if (/focus-visible/.test(selector)) return;
      offenders.push(`${path}:${index + 1} under "${selector}"`);
    });
  }

  it("reads the whole style layer", () => {
    expect(files.length).toBeGreaterThan(50);
  });

  it("is never taken away from the keyboard", () => {
    expect(offenders.sort()).toEqual([]);
  });

  it("is declared for form controls and not only for links and buttons", () => {
    const reset = readFileSync(
      join(CLIENT_SRC, "assets/styles/Reset.sass"),
      "utf8",
    );
    for (const control of ["input", "textarea", "select"]) {
      expect(reset).toContain(`${control}:focus-visible`);
    }
  });
});
