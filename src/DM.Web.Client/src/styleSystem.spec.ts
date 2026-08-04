/**
 * @vitest-environment node
 */

/**
 * The shared style layer owns values a component may not re-decide, and every
 * one of them was re-decided the same way: a number written where a token
 * belongs. A number cannot be wrong on its own — it is wrong only next to the
 * scale it disagrees with, which is why none of these showed up until something
 * ended up in the wrong order or in the wrong place.
 *
 * z-index. The scale in _ZIndex.sass is a document about what sits over what. A
 * literal silently outranks it: a tooltip at 10001 covered the toast tier at
 * 10000, which the scale marks "always on top", so a [tip:] opened while an
 * error toast was on screen hid the error. Local stacking inside one component
 * (-1, 0, 1, 2) is not the scale's business and stays allowed.
 *
 * Breakpoints. Six competing values under three names. Three neighbouring
 * blocks reflowed at 600, 620 and 640, so a tablet in portrait broke the page
 * in three jerks instead of one, and DataTable and its own skeleton each held a
 * private copy of 768 — change one, and the table parts from its placeholder.
 *
 * Global rules. A partial that emits a rule is emitted again into every scoped
 * stylesheet that imports it. InputsGlobal.sass records in bytes what that cost
 * the last time; @keyframes skeleton-shimmer was shipping twenty copies of
 * itself for the same reason, eight of them in the entry chunk.
 *
 * Tap targets. WCAG 2.5.8 puts the floor for a pointer target at 24x24 CSS px,
 * and App.vue writes the project's own stricter norm in a comment. Three
 * icon-only controls sat under even the softened floor. The glyph is allowed to
 * stay small — the mixin grows the target around it — so what is checked is
 * that those controls take the mixin and that the mixin still declares a floor.
 *
 * Assets. A file under assets/ that nothing references is weight in every clone
 * and a candidate for being "used somewhere" forever. Five of them, 794 KB,
 * were reachable from nothing at all.
 *
 * Colour. A hex outside assets/styles/ is a colour that belongs to one theme:
 * the palettes are switched by a class on <html>, and a literal is not. The two
 * that were on the tree said so — a tooltip's error message in a red nobody
 * picked, and a sample of the current button drawn in white letters that stayed
 * white when the page went dark.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { basename, dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const STYLE_FILES = [".vue", ".sass", ".scss", ".css"];

function collect(
  dir: string,
  wanted: (name: string) => boolean,
  out: string[] = [],
): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, wanted, out);
    } else if (wanted(name)) {
      out.push(full);
    }
  }
  return out;
}

const isStyle = (name: string): boolean =>
  STYLE_FILES.some((ext) => name.endsWith(ext));

const styleFiles = (): string[] => collect(CLIENT_SRC, isStyle);

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

/** Values that order children INSIDE one stacking context, not on the scale. */
const LOCAL_STACKING = new Set([-1, 0, 1, 2]);

const Z_LITERAL = /^\s*z-index:\s*(-?\d+)\s*;?\s*$/gm;

const MEDIA_LITERAL = /@media[^\n{]*\((?:max|min)-width:\s*(\d+)px/g;

/** A colour written as itself: #abc, #aabbcc, and the alpha-carrying forms. */
const HEX_LITERAL = /#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b/;

/** Where a literal width may still stand, and what it is. */
const MEDIA_LITERAL_ALLOWED: Record<string, string> = {
  "widgets/header/Header.vue":
    "three steps of progressive menu compression (1699/1489/1339), not layout breakpoints — the file says so where they are declared",
};

/** Stylesheets main.ts imports, i.e. the ones emitted exactly once. */
const GLOBAL_SHEETS = new Set(
  [
    ...readFileSync(join(CLIENT_SRC, "app/main.ts"), "utf8").matchAll(
      /^import "@\/(assets\/styles\/[\w.-]+)";$/gm,
    ),
  ].map((found) => found[1]),
);

/** Icon-only controls that were under the floor, and are held above it. */
const ICON_ONLY = [
  "shared/ui/Filters/primitives/FilterDropdownHeader.vue",
  "pages/profile/ProfilePersonalInfo.vue",
  "shared/ui/ScrollNav/ScrollNav.vue",
];

describe("the z-index scale", () => {
  it("is where a stacking order above the local one comes from", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const source = readFileSync(file, "utf8");
      for (const [, value] of source.matchAll(Z_LITERAL)) {
        if (LOCAL_STACKING.has(Number(value))) continue;
        offenders.push(
          `${asPath(file)}: z-index: ${value} is outside assets/styles/_ZIndex.sass, so nothing keeps it in the order the scale documents`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("the breakpoint scale", () => {
  it("is where a layout reflow point comes from", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      if (path in MEDIA_LITERAL_ALLOWED) continue;
      const source = readFileSync(file, "utf8");
      for (const [, value] of source.matchAll(MEDIA_LITERAL)) {
        offenders.push(
          `${path}: @media at ${value}px instead of a token from assets/styles/_Breakpoints.sass`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });

  it("has no second name for any of its steps", () => {
    // $min-width and $mobile-breakpoint were aliases of $bp-shell and
    // $bp-mobile, declared in two unrelated partials and used from four files
    // that had no idea they were on the scale at all.
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const source = readFileSync(file, "utf8");
      for (const alias of ["$min-width", "$mobile-breakpoint"]) {
        if (!source.includes(alias)) continue;
        offenders.push(
          `${asPath(file)}: ${alias} is a second name for a step of the scale`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("a global rule", () => {
  it("reads the sheets main.ts emits once", () => {
    expect(GLOBAL_SHEETS.size).toBeGreaterThan(3);
  });

  it("is emitted once and not per importing stylesheet", () => {
    const offenders: string[] = [];
    for (const file of collect(join(CLIENT_SRC, "assets/styles"), isStyle)) {
      // Comments stripped first. The check is textual, so a partial that only
      // says in a comment where the rule lives read as a partial declaring it,
      // and the way out was to reword the sentence rather than to move any CSS.
      const source = readFileSync(file, "utf8").replace(/^\s*\/\/.*$/gm, "");
      if (!source.includes("@keyframes")) continue;
      const path = asPath(file);
      if (GLOBAL_SHEETS.has(path)) continue;
      offenders.push(
        `${path}: declares @keyframes in a partial, so every scoped stylesheet that imports it ships its own copy`,
      );
    }
    expect(offenders).toEqual([]);
  });
});

describe("the pointer target of an icon-only control", () => {
  it("has a floor declared in one place", () => {
    const variables = readFileSync(
      join(CLIENT_SRC, "assets/styles/Variables.sass"),
      "utf8",
    );
    expect(variables).toMatch(/\$tap-target-min:\s*24px/);

    const inputs = readFileSync(
      join(CLIENT_SRC, "assets/styles/Inputs.sass"),
      "utf8",
    );
    expect(inputs).toContain("@mixin icon-button(");
    expect(inputs).toContain("$tap-target-min");
  });

  it("is taken from that place by the controls that were under it", () => {
    const offenders = ICON_ONLY.filter(
      (path) =>
        !readFileSync(join(CLIENT_SRC, path), "utf8").includes("+icon-button"),
    ).map(
      (path) =>
        `${path}: an icon-only control sizing its own hit box again — the three that did measured 20x20, 20x20 and 36x18`,
    );
    expect(offenders).toEqual([]);
  });
});

describe("a colour outside the palettes", () => {
  it("is a token and not a literal", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      // assets/styles/ IS the palettes: ThemeVariables.css is where the hexes
      // of both themes are declared and calibrated against each other.
      if (path.startsWith("assets/styles/")) continue;
      const source = readFileSync(file, "utf8");
      // A .vue file's script may legitimately carry a hex — App.vue mirrors
      // --bg-page into the theme-color meta, which is not a stylesheet value.
      const styles = path.endsWith(".vue")
        ? [...source.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)]
            .map((found) => found[1])
            .join("\n")
        : source;
      for (const line of styles
        .replace(/\/\*[\s\S]*?\*\//g, "")
        .split("\n")
        .map((one) => one.replace(/\/\/.*$/, ""))) {
        if (!HEX_LITERAL.test(line)) continue;
        offenders.push(
          `${path}: ${line.trim()} — a hex belongs to one theme, and the theme is switched by a class on <html>`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("an asset in the tree", () => {
  it("is reachable from the code", () => {
    const haystack = [
      ...collect(CLIENT_SRC, (name) =>
        [".ts", ".vue", ".sass", ".scss", ".css"].some((ext) =>
          name.endsWith(ext),
        ),
      ),
      join(CLIENT_SRC, "..", "index.html"),
    ]
      .map((file) => readFileSync(file, "utf8"))
      .join("\n");

    const assets = [
      ...collect(join(CLIENT_SRC, "assets/images"), () => true),
      ...collect(join(CLIENT_SRC, "assets/fonts"), () => true),
    ];
    expect(assets.length).toBeGreaterThan(5);

    const offenders = assets
      .filter((file) => !haystack.includes(basename(file)))
      .map((file) => `${asPath(file)}: nothing in the client names this file`);
    expect(offenders).toEqual([]);
  });
});

describe("the profile avatar slot", () => {
  it("reserves the height it is drawn at", () => {
    // The API sends no intrinsic size for the aspect-preserving original, so
    // AvatarImg declares a square and the slot holds that square as a floor.
    // Without it a landscape picture shrank the box on decode and pulled the
    // role line, the statistics and the tabs up with it.
    const source = readFileSync(
      join(CLIENT_SRC, "pages/profile/ProfilePage.vue"),
      "utf8",
    );
    const block = /\n\.avatar-wrapper\n([\s\S]*?)\n\n/.exec(source);
    expect(
      block,
      "ProfilePage.vue has no .avatar-wrapper block",
    ).not.toBeNull();
    expect(block?.[1]).toMatch(/min-height:\s*220px/);
  });
});
