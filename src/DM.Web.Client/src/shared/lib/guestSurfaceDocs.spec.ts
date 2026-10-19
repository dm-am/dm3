/**
 * @vitest-environment node
 */

// The interface standards on the guest screens against the screens: only what
// the code can answer, a label, a control, a number of the stylesheet.
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import { SURFACE } from "@/shared/lib/testing/guestSheet";

const HERE = dirname(fileURLToPath(import.meta.url));
// lib -> shared -> src
const SRC_ROOT = resolve(HERE, "..", "..");
// src -> DM.Web.Client -> src -> repository root
const DOCS = resolve(SRC_ROOT, "..", "..", "..", "docs");

// The standards are an index and its parts: each block reads the part its section is in.
const DIALOG_STANDARDS = resolve(
  DOCS,
  "conventions",
  "UI_STANDARDS",
  "dialogs.md",
);
const SURFACE_STANDARDS = resolve(
  DOCS,
  "conventions",
  "UI_STANDARDS",
  "surfaces.md",
);

const read = (file: string) => readFileSync(file, "utf8");

/** Everything under a heading, up to the next heading of the same level. */
function section(text: string, heading: string): string {
  const start = text.indexOf(heading);
  if (start < 0) return "";
  const level = heading.slice(0, heading.indexOf(" "));
  const rest = text.slice(start + heading.length);
  const end = rest.indexOf(`\n${level} `);
  return end < 0 ? rest : rest.slice(0, end);
}

// ─────────────────────────────────────────── the guest screens themselves ──

const GUEST_UI = join(SRC_ROOT, "features", "auth", "ui");
const AUTH_TOKENS = join(SRC_ROOT, "assets", "styles", "_AuthTokens.sass");
const AUTH_DIALOG = join(SRC_ROOT, "shared", "ui", "Layout", "AuthDialog.vue");
const VARIABLES = join(SRC_ROOT, "assets", "styles", "Variables.sass");

/** The value of a sass variable, as written. */
function sassValue(file: string, name: string): string {
  const match = new RegExp(`^\\${name}:\\s*(\\S+)`, "m").exec(read(file));
  return match ? match[1] : "";
}

// ─────────────────────────────────────────────────── the dialog standards ──

describe("the dialog standards on the guest dialogs", () => {
  const standards = read(DIALOG_STANDARDS);
  const dialogs = section(standards, "## Dialog Standards");
  const shell = read(AUTH_DIALOG);

  it("reads the section it means", () => {
    expect(dialogs.length).toBeGreaterThan(500);
  });

  it("names the shell the guest dialogs open in", () => {
    expect(
      dialogs.includes("AuthDialog"),
      "the cancel rule is written here, and the contour that has no cancel button has to be named where the rule is",
    ).toBe(true);
  });

  it("quotes the label the guest way out actually carries", () => {
    // The way out of a guest dialog is the cross drawn of two bars; the
    // standards quote the label it carries.
    const label = /class="auth-close"[\s\S]*?aria-label="([^"]+)"/.exec(shell);

    expect(label, "the guest dialog closes by a labelled cross").not.toBeNull();
    expect(
      dialogs.includes(`aria-label="${label?.[1]}"`),
      `the standards must quote the label the cross carries (${label?.[1]})`,
    ).toBe(true);
  });

  it("finds no cancel control on the guest surface", () => {
    const offenders: string[] = [];
    for (const name of readdirSync(GUEST_UI)) {
      if (!name.endsWith(".vue")) continue;
      const source = read(join(GUEST_UI, name));
      if (!source.includes("<AuthDialog")) continue;
      // A label, not the word in a comment or in a handler name.
      if (/>\s*Отмена\s*<|(["'`])Отмена\1/.test(source)) offenders.push(name);
    }

    expect(
      offenders,
      'the guest dialogs close by the cross, Escape and a click outside: a button labelled "Отмена" would contradict the rule the standards record',
    ).toEqual([]);
  });
});

// ────────────────────────────────────────────────── the surface standards ──

describe("the surface standards on the guest surface", () => {
  const standards = read(SURFACE_STANDARDS);
  const surfaces = section(standards, "## Surface Standards");
  const sheet = SURFACE;

  it("reads the section it means", () => {
    expect(surfaces.length).toBeGreaterThan(300);
  });

  it("names the third family and the sheet that draws it", () => {
    expect(surfaces).toContain(".auth-surface");
    expect(surfaces).toContain("AuthSurface.sass");
  });

  // The numbers of the guest language live in the sheet, not in the standards.
  it("carries the rule instead of the numbers behind it", () => {
    const sheetValues = ["$auth-control-height", "$auth-strip"]
      .map((name) => sassValue(AUTH_TOKENS, name))
      .concat(sassValue(VARIABLES, "$title-font-size"));

    expect(sheet).toContain("font-size: $title-font-size");
    for (const value of sheetValues) {
      expect(
        surfaces.includes(value),
        `the standards quote ${value}, which belongs to the sheet: repeat it here and the two go apart`,
      ).toBe(false);
    }
  });
});
