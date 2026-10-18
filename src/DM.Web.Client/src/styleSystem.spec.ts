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
 * One edge. A rule in an accent down a single side is a mark the site spends
 * on two things in post text: a quote and a [mod] block. It had spread to six
 * more places - the toast, a review, the security log, a support thread, a
 * notepad entry and a private message - where it read as a second kind of
 * card competing with the frame around it. The kind of a panel is its tint and
 * its whole frame; a state is the tint the rest of the site pulses.
 *
 * Colour. A hex outside assets/styles/ is a colour that belongs to one theme:
 * the palettes are switched by a class on <html>, and a literal is not. The two
 * that were on the tree said so — a tooltip's error message in a red nobody
 * picked, and a sample of the current button drawn in white letters that stayed
 * white when the page went dark.
 *
 * The controls of a page (owner's standard, 28 September 2026, after the 32 of
 * the day before was turned down). Every button and every field of one line on
 * a page is drawn after the filter bar of the games list as it stood before:
 * the search, "Фильтры" and the sort at 38, in 14, the field 8 in from its
 * frame on the ground of $bg-element, the button 16 to each side, one radius.
 * A window keeps its 52 and a text area grows with its rows. The caption over
 * a field is bold wherever a page draws one: regular grey captions stood over
 * one form and bold ones over the next. A card of content is square and a
 * control is rounded by the one radius of the site; a literal radius is a
 * named exception or a defect. And a link answers the pointer one way, an <a>
 * and a <button> alike: .4s on one and nothing on the other stood side by side
 * in the header.
 *
 * No cut (owner, 12 October 2026). A phone shows the full version, whole: a
 * name, a title or a caption that does not fit wraps, and nothing ends in "…"
 * for want of room. What still cuts stands in a register with its reason and
 * the task that removes it; a text cut by the edge of its box is caught on the
 * page, by the e2e of nothing hidden or cut.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { basename, dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import {
  compileSass,
  componentCss,
  ruleMap,
  sassBlocks,
  sassPrelude,
  styleRules,
} from "@/shared/lib/testing/styles";

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
    "the steps of the menu's type, one set for eight links and one for nine, not layout breakpoints — the file says so where they are declared, and widgets/header/menuSteps.spec.ts measures them",
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
  // The cross of a filter bubble, 14 by 14 (=remove-icon-btn).
  "assets/styles/_Filters.sass",
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
  const profilePage = () =>
    readFileSync(join(CLIENT_SRC, "pages/profile/ProfilePage.vue"), "utf8");

  it("reserves no height of its own for a picture whose size is known", () => {
    // The reservation belongs on the img, where width/height give the browser
    // the real ratio before the decode. A floor on the wrapper cannot know that
    // ratio, so it reserved a 220px square for a 220x165 picture and left 55px
    // blank under every landscape avatar.
    const block = /\n\.avatar-wrapper\n([\s\S]*?)\n\n/.exec(profilePage());
    expect(
      block,
      "ProfilePage.vue has no .avatar-wrapper block",
    ).not.toBeNull();
    expect(block?.[1]).not.toMatch(/min-height/);
  });

  it("still holds the square when nobody measured the picture", () => {
    // An upload made before the pipeline recorded its dimensions leaves
    // AvatarImg declaring a square, and the square then has to be held by the
    // slot: without it the box shrinks on decode and pulls the role line, the
    // statistics and the tabs up with it.
    const source = profilePage();
    const block = /\n\.avatar-wrapper-unsized\n([\s\S]*?)\n\n/.exec(source);
    expect(
      block,
      "ProfilePage.vue has no .avatar-wrapper-unsized block",
    ).not.toBeNull();
    expect(block?.[1]).toMatch(/min-height:\s*220px/);
    expect(
      source,
      "the modifier has to be bound, or the floor is unreachable",
    ).toContain("'avatar-wrapper-unsized': !avatarSizeIsKnown");
  });
});

describe("the site checkbox", () => {
  it("zeroes the field padding it also inherits, so it is the square it asks for", () => {
    // +checkbox() asks for a 16px square, but the same <input> goes through
    // +input() as well (InputsGlobal puts every input through it) and arrives
    // with a text field's padding. A border-box control cannot be narrower
    // than its own padding and edges, so every checkbox on the site measured
    // 18x16 - a squat rectangle under a centred tick - until the mixin zeroed
    // it. The side lives once, as $checkbox-size, so nothing measures the box
    // by eye again.
    const inputs = readFileSync(
      join(CLIENT_SRC, "assets/styles/Inputs.sass"),
      "utf8",
    );
    const from = inputs.indexOf("@mixin checkbox()");
    expect(from).toBeGreaterThan(-1);
    const next = inputs.indexOf("\n@mixin ", from + 1);
    const body = inputs.slice(from, next > 0 ? next : undefined);
    expect(body).toMatch(/^\s+padding: 0$/m);
    expect(body).toMatch(/^\s+width: \$checkbox-size$/m);
    expect(body).toMatch(/^\s+height: \$checkbox-size$/m);
    expect(inputs).toMatch(/^\$checkbox-size:\s*16px$/m);
  });

  it("is measured by the guest check line through its name, not by eye", () => {
    // The check line hangs its indent on the box. It used to write the leaked
    // width out (18px, with a note explaining the leak) instead of fixing it.
    const auth = readFileSync(
      join(CLIENT_SRC, "assets/styles/AuthSurface.sass"),
      "utf8",
    );
    expect(auth).toMatch(/^\$auth-check-box:\s*\$checkbox-size$/m);
  });
});

/** An accent as a value: the palette's own hues, and a saturated literal. */
const ACCENT_VALUE =
  /\$(?:accent-|border-accent-|link\b|highlight-overlay-)|var\(--(?:accent-|border-accent-|link\b)/;

/** A single side, written as a border or as an inset shadow along one axis. */
const ONE_EDGE =
  /border-(?:left|right|top|bottom|inline-start|inline-end|block-start|block-end)(?:-color)?:\s*([^\n;]+)|box-shadow:\s*inset\s+(-?[\d.]+px\s+0\s+[^\n;]+|0\s+-?[\d.]+px\s+[^\n;]+)/g;

/** A literal that carries a hue, as opposed to a grey or a white veil. */
const isColoured = (value: string): boolean => {
  if (ACCENT_VALUE.test(value)) return true;
  const hex = value.match(/#([0-9a-fA-F]{3,8})\b/);
  if (hex) {
    const full =
      hex[1].length <= 4
        ? hex[1]
            .slice(0, 3)
            .split("")
            .map((one) => one + one)
            .join("")
        : hex[1].slice(0, 6);
    const [r, g, b] = [0, 2, 4].map((at) =>
      parseInt(full.slice(at, at + 2), 16),
    );
    return !(r === g && g === b);
  }
  const rgb = value.match(/rgba?\(\s*(\d+)[\s,]+(\d+)[\s,]+(\d+)/);
  if (rgb) return !(rgb[1] === rgb[2] && rgb[2] === rgb[3]);
  return false;
};

describe("a coloured edge down one side", () => {
  it("is the mark of a quote and of a [mod] block, and of nothing else", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      // The two blocks the mark belongs to are drawn here, and the guest sheet
      // draws the one horizontal rule of a footnote in the neutral border.
      if (path === "assets/styles/_BbcodeContent.sass") continue;
      const source = readFileSync(file, "utf8");
      const styles = path.endsWith(".vue")
        ? [...source.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)]
            .map((found) => found[1])
            .join("\n")
        : source;
      const clean = styles
        .replace(/\/\*[\s\S]*?\*\//g, "")
        .split("\n")
        .map((one) => one.replace(/\/\/.*$/, ""))
        .join("\n");
      for (const found of clean.matchAll(ONE_EDGE)) {
        const value = (found[1] ?? found[2]).trim();
        if (!isColoured(value)) continue;
        offenders.push(
          `${path}: ${found[0].trim()} — the kind of a panel is its tint and its whole frame, not a rule down one side`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("a control on a page", () => {
  const probe = (block: string) =>
    ruleMap(compileSass(`@use "@/assets/styles/Inputs" as *\n${block}`));
  const variables = () =>
    readFileSync(join(CLIENT_SRC, "assets/styles/Variables.sass"), "utf8");

  it("is 38 high in 14, the one measure of the page", () => {
    // The filter bar of the games list as it stood before the 32 of 27
    // September, which the owner turned down the next day.
    expect(variables()).toMatch(/^\$control-height:\s*38px$/m);
    expect(variables()).toMatch(
      /^\$control-font-size:\s*\$secondary-font-size$/m,
    );
    expect(variables()).toMatch(/^\$field-inset:\s*\$small$/m);
    expect(variables()).toMatch(/^\$button-padding:\s*0 \$medium$/m);
  });

  it("is a field of one line at that height by declaration, 8 in from its frame, in 14", () => {
    // Declared, and not left to the padding and the font's line box, which
    // put the field near the token by accident and moved it with the font.
    const rules = probe(".probe\n  +input");
    const line = [...rules.entries()].find(([selector]) =>
      selector.startsWith(".probe:where(:not(textarea"),
    );
    expect(line, "+input has no rule for a field of one line").toBeDefined();
    expect(line?.[1]).toMatchObject({
      "box-sizing": "border-box",
      height: "38px",
      padding: "0 8px",
      "font-size": "14px",
    });
    // A text area and the boxes a reader ticks keep their own geometry, and
    // the text area its 16: a post, a note, the text of a tooltip.
    for (const kept of ["textarea", "[type=checkbox]", "[type=radio]"])
      expect(line?.[0], kept).toContain(kept);
    expect(rules.get(".probe")?.["font-size"]).toBe("16px");
  });

  it("is a search of a filter bar as it stood before, the measure of every field", () => {
    const rules = ruleMap(
      compileSass(
        '@use "@/assets/styles/Filters" as *\n.probe\n  +filter-search-container',
      ),
    );
    expect(rules.get(".probe .search-container")).toMatchObject({
      height: "38px",
      "font-size": "14px",
      padding: "0 8px",
      border: "1px solid var(--border)",
      "border-radius": "8px",
      // Over the page this is $bg-element, the ground the search stood on
      // (fillContrast.spec.ts measures that); one ground for every field.
      "background-color": "var(--input-bg-overlay)",
    });
    expect(rules.get(".probe .the-input")?.["font-size"]).toBe("14px");
    const field = probe(".probe\n  +input-base").get(".probe");
    expect(field).toMatchObject({
      "background-color": "var(--input-bg-overlay)",
      border: "1px solid var(--border)",
      "border-radius": "8px",
    });
  });

  it("is a date field of the page at the page's height and type", () => {
    const sheet = ruleMap(
      componentCss(join(CLIENT_SRC, "shared/ui/DatePicker/DateInput.vue")),
    );
    expect(sheet.get(".di-field")?.height).toBe("38px");
    expect(sheet.get(".di-input")?.["font-size"]).toBe("14px");
    expect(sheet.get(".di-input")?.padding).toBe("0 2px 0 8px");
  });

  it("is the page's field in the panel of a filter too, level with its buttons", () => {
    // The panel kept a measure of its own, 36 from the padding, beside the 38
    // of "Неделя", "Месяц", "Год" and "Применить" (28 September 2026).
    const panel = ruleMap(
      compileSass(
        '@use "@/assets/styles/Filters" as *\n.probe\n  +dropdown-search-input',
      ),
    ).get(".probe .dropdown-search-input");
    expect(panel).toMatchObject({
      height: "38px",
      padding: "0 8px",
      "font-size": "14px",
      "background-color": "var(--input-bg-overlay)",
    });
    // The dates of the range are the page's date field, not its compact
    // variant, which drew them 36.
    const range = readFileSync(
      join(CLIENT_SRC, "shared/ui/Filters/inputs/DateRangePicker.vue"),
      "utf8",
    );
    expect(range).toMatch(/<DateInput\b/);
    expect(range).not.toMatch(/<DateInput\b[^>]*\scompact\b/);
    // The numbers of a range, the same field with a step on either side.
    const stepper = ruleMap(
      componentCss(
        join(CLIENT_SRC, "shared/ui/Filters/inputs/NumericRangePicker.vue"),
      ),
    );
    expect(stepper.get(".stepper")).toMatchObject({
      height: "38px",
      "box-sizing": "border-box",
      "background-color": "var(--input-bg-overlay)",
    });
    expect(stepper.get(".stepper-input")?.["font-size"]).toBe("14px");
  });

  it("is a bubble of an applied filter 28 high, with a cross a finger can take", () => {
    // Every bubble of a row stands at 28, the bubble of names and "и еще N"
    // too: the line of 14 in the middle of the 26 between the frames, and the
    // pill of a name as high as that inside.
    const bubbles = ruleMap(
      compileSass(
        '@use "@/assets/styles/Filters" as *\n.probe\n  +filter-bubbles',
      ),
    );
    const pill = ruleMap(
      componentCss(join(CLIENT_SRC, "entities/game/ui/TagSelector.vue")),
    ).get(".tag-button");
    const bubble = bubbles.get(".probe .bubble");
    for (const size of ["font-size", "border"])
      expect(bubble?.[size], size).toBe(pill?.[size]);
    expect(bubble).toMatchObject({
      "box-sizing": "border-box",
      "min-height": "28px",
      padding: "0 8px",
      "line-height": "26px",
    });
    expect(bubbles.get(".probe .bubble-owner-item")).toMatchObject({
      padding: "2px 4px",
      margin: "-2px 0",
      "line-height": "22px",
    });
    // The copy space would take the line of 26 and grow the bubble.
    expect(bubbles.get(".probe .bubble .copy-space")?.["line-height"]).toBe(
      "0",
    );
    // The cross is drawn 14 and taken at the floor of a target, 24.
    expect(bubbles.get(".probe .bubble-remove-btn")).toMatchObject({
      width: "14px",
      height: "14px",
      position: "relative",
    });
    expect(bubbles.get(".probe .bubble-remove-btn::after")).toMatchObject({
      width: "max(100%, 24px)",
      height: "max(100%, 24px)",
    });
  });

  it("is a button of the filter bar's geometry, beside a pill and a field of the same", () => {
    const segmented = ruleMap(
      componentCss(
        join(CLIENT_SRC, "shared/ui/SegmentedControl/SegmentedControl.vue"),
      ),
    );
    expect(segmented.get(".segmented-control")?.height).toBe("38px");
    const button = probe(".probe\n  +button");
    expect(button.get(".probe")).toMatchObject({
      height: "38px",
      padding: "0 16px",
      "font-size": "14px",
      "border-radius": "8px",
    });
    // The main action is the same box in another colour: the modifier names
    // colours and the weight, never a size of its own.
    const primary = probe(".probe\n  +button-primary").get(".probe") ?? {};
    for (const size of ["height", "padding", "font-size", "border-radius"])
      expect(primary[size], size).toBeUndefined();
  });
});

/** Captions of a field that a page draws by hand, and not through FormField. */
const HAND_CAPTIONS: { file: string; selector: string }[] = [
  { file: "pages/game/GameRoom.vue", selector: ".composer-label" },
  { file: "pages/game/GameRoom.vue", selector: ".dice-title" },
  { file: "pages/game/GameRoom.vue", selector: ".attachments-title" },
  { file: "pages/game/GameRoom.vue", selector: ".turns-title" },
  {
    file: "widgets/game-post/DiceComposer.vue",
    selector: ".dice-comment-caption",
  },
  {
    file: "pages/game/settings/RoomsSection.vue",
    selector: ".grants-caption",
  },
  { file: "widgets/game-post/GamePost.vue", selector: ".edit-label" },
  { file: "features/topic/ui/TopicView.vue", selector: ".edit-label" },
  { file: "features/poll-vote/ui/PollEditForm.vue", selector: ".edit-label" },
  { file: "pages/game/CharacterEdit.vue", selector: ".switch-label" },
  {
    file: "pages/moderation/FundraisingPage.vue",
    selector: ".form-field label",
  },
  {
    file: "pages/moderation/ModerationAwardsSeries.vue",
    selector: ".form-row label",
  },
];

describe("the caption of a field on a page", () => {
  it("is 14 and bold in the ink of the text, declared once", () => {
    const caption = ruleMap(
      compileSass(
        '@use "@/assets/styles/Inputs" as *\n.probe\n  +field-caption',
      ),
    ).get(".probe");
    expect(caption).toEqual({
      color: "var(--text)",
      "font-size": "14px",
      "font-weight": "700",
    });
  });

  it("is what FormField draws over a field of a page", () => {
    const sheet = ruleMap(
      componentCss(join(CLIENT_SRC, "shared/ui/Form/FormField.vue")),
    );
    const found = sheet.get(".form-field-label");
    expect(found, "FormField.vue has no .form-field-label").toBeDefined();
    expect(found).toMatchObject({
      color: "var(--text)",
      "font-size": "14px",
      "font-weight": "700",
    });
  });

  it("is the same where a page captions a field by hand", () => {
    // Regular grey captions stood over the room's composer and the edit of a
    // post while FormField drew them bold one form away.
    const offenders: string[] = [];
    for (const { file, selector } of HAND_CAPTIONS) {
      const sheet = ruleMap(componentCss(join(CLIENT_SRC, file)));
      const found = sheet.get(selector);
      if (!found) {
        offenders.push(`${file}: no ${selector}`);
        continue;
      }
      const { color, "font-size": size, "font-weight": weight } = found;
      if (color !== "var(--text)" || size !== "14px" || weight !== "700")
        offenders.push(
          `${file} ${selector}: ${color} ${size} ${weight}, a caption of a page is var(--text) 14px 700`,
        );
    }
    expect(offenders).toEqual([]);
  });
});

/** What a rounded corner may be written as without a word of explanation. */
const RADIUS_TOKENS = new Set([
  "$border-radius",
  "$button-border-radius",
  "$bubble-radius",
  "inherit",
  "0",
]);

/**
 * Every other radius on the tree, file by file and value by value, with the
 * reason it is not the site's one. A control is rounded by $border-radius and
 * a card of content not at all; what stands here is neither, or it is a defect
 * that lives in a zone of its own and is named as one.
 */
const RADIUS_REGISTER: Record<
  string,
  { values: string[]; why: string; open?: true }
> = {
  "assets/styles/Inputs.sass": {
    values: ["2px", "50%"],
    why: "the checkbox, 2 on a side of 16 (the site's 8 would draw it round, as the radio), and the radio with its dot",
  },
  "assets/styles/AuthSurface.sass": {
    values: ["1px", "2px"],
    why: "the guest's check box and the bars of the marks it draws",
  },
  "assets/styles/_DialogSurface.sass": {
    values: ["1px"],
    why: "the two bars of a window's cross",
  },
  "assets/styles/_Skeleton.sass": {
    values: ["$minor"],
    why: "a shimmering line of text waiting to load, not a control",
  },
  "assets/styles/Reset.sass": {
    values: ["3px"],
    why: "the marker pen over the letters of a search match",
  },
  "entities/testimonial/ui/TestimonialSkeleton.vue": {
    values: ["3px"],
    why: "a shimmering line of text waiting to load",
  },
  "pages/account/AccountActivationPage.vue": {
    values: ["1px"],
    why: "the bars of a drawn mark",
  },
  "pages/account/UsernameChangePage.vue": {
    values: ["1px"],
    why: "the bars of a drawn mark",
  },
  "widgets/chat-feed/ChatFeed.vue": {
    values: ["7px"],
    why: "the thumb of the feed's scrollbar, in both chats",
  },
  "pages/moderation/ModerationAchievements.vue": {
    values: ["3px"],
    why: "a code of the catalogue set in monospace: text, not a control",
  },
  "pages/moderation/ModerationAwardTypes.vue": {
    values: ["3px"],
    why: "a code of the catalogue set in monospace: text, not a control",
  },
  "pages/profile/ProfileAchievementsSection.vue": {
    values: ["$minor", "3px"],
    why: "the tier badge on an icon and the 6px bar of a chain's progress",
  },
  "pages/profile/ProfileAwardsSection.vue": {
    values: ["$minor"],
    why: "the tier badge on an icon",
  },
  "pages/rules/RulesIntro.vue": {
    values: ["2px"],
    why: "a tag of the markup quoted in the text, set as code",
  },
  "shared/ui/LikeBadge/LikeBadge.vue": {
    values: ["$minor"],
    why: "the plate of the count on the heart",
  },
  "shared/ui/PasswordInput/PasswordStrengthIndicator.vue": {
    values: ["2px"],
    why: "the bars of the strength scale",
  },
  "shared/ui/SiteSettings/SiteSettingsPanel.vue": {
    values: ["18px", "50%"],
    why: "the track of a switch and its knob",
  },
  "widgets/footer/Footer.vue": {
    values: ["50%"],
    why: "the ring of the age mark, text and not a control",
  },
  "widgets/sidebar/PartnersPanel.vue": {
    values: ["50%"],
    why: "the round logos of the partners, the one picture of the site that is rounded (owner, 12 October 2026)",
  },
  "shared/ui/BBCodeEditor/BBCodeEditor.vue": {
    values: ["2px"],
    why: "the tag buttons of the toolbar, drawn as DM2's and as before 28 September: the site's 8 rounded them into controls of another kind (owner, 5 October 2026)",
  },
  "pages/moderation/dialogs/HintField.vue": {
    values: ["2px"],
    why: "the tag buttons over the hint field, the editor's toolbar buttons (BBCodeEditor.vue)",
  },
};

/** The radii a style file declares, comments left out. */
function radii(file: string): string[] {
  const path = asPath(file);
  const source = readFileSync(file, "utf8");
  const styles = path.endsWith(".vue")
    ? [...source.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)]
        .map((found) => found[1])
        .join("\n")
    : source;
  const clean = styles
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .split("\n")
    .map((one) => one.replace(/\/\/.*$/, ""))
    .join("\n");
  return [...clean.matchAll(/^\s*border-radius:\s*([^\n;]+)/gm)].map((found) =>
    found[1].trim(),
  );
}

describe("a rounded corner", () => {
  it("is the site's one radius, or an exception with a reason", () => {
    // A control wore 2, 3, 50% and 999px beside the 8 of the next one, and a
    // message of the chat a highlight rounded on one side (28 September).
    const offenders: string[] = [];
    let seen = 0;
    for (const file of styleFiles()) {
      const path = asPath(file);
      for (const value of radii(file)) {
        seen += 1;
        if (RADIUS_TOKENS.has(value)) continue;
        if (RADIUS_REGISTER[path]?.values.includes(value)) continue;
        offenders.push(
          `${path}: border-radius: ${value} — a control takes $border-radius, a card of content none; anything else goes into RADIUS_REGISTER with its reason`,
        );
      }
    }
    expect(offenders).toEqual([]);
    expect(seen).toBeGreaterThan(50);
  });

  it("keeps no reason for a radius that is gone", () => {
    // An open defect is left out: it goes from the register by hand when its
    // zone fixes it, and a fix elsewhere must not turn this file red.
    const stale: string[] = [];
    for (const [path, { values, open }] of Object.entries(RADIUS_REGISTER)) {
      if (open) continue;
      const found = radii(join(CLIENT_SRC, path));
      for (const value of values)
        if (!found.includes(value)) stale.push(`${path}: ${value}`);
    }
    expect(stale).toEqual([]);
  });

  it("is the site's one radius on the controls that wore another", () => {
    // The chevron of a filter bubble (2), the round cross of the notepad
    // (50%), the sign buttons of a post's rating (none) and
    // the badge of a tooltip (999px). Named, so a regression reads as the
    // control it hit. The pills of the account's sections are gone: a state is
    // a word there, with no corner to round.
    const fixed: [string, string][] = [
      ["assets/styles/_Filters.sass", ".bubble-expand-btn"],
      ["widgets/notepad/NotepadBoard.vue", ".close-btn"],
    ];
    for (const [file, selector] of fixed) {
      const css = file.endsWith(".vue")
        ? // Compiled as the file, so its own relative @use lines resolve.
          compileSass(
            sassBlocks(join(CLIENT_SRC, file)),
            join(CLIENT_SRC, file),
          )
        : compileSass(
            `@use "@/assets/styles/Filters" as *\n.probe\n  +filter-bubbles`,
          );
      const rule = [...ruleMap(css).entries()].find(
        ([one]) => one === selector || one === `.probe ${selector}`,
      );
      expect(rule, `${file}: no ${selector}`).toBeDefined();
      expect(rule?.[1]["border-radius"], `${file} ${selector}`).toBe("8px");
    }
  });

  it("is DM2's 2 on the tag buttons of the editor, not the site's 8", () => {
    // The wave of 28 September gave them the token of a control, and the row
    // of the toolbar read as a row of pills (owner, 5 October 2026). Named
    // here and not only in the register: the token passes the first test of
    // this block, so a regression would only show as a stale register entry,
    // which reads as tidying up.
    const file = join(CLIENT_SRC, "shared/ui/BBCodeEditor/BBCodeEditor.vue");
    const rule = ruleMap(compileSass(sassBlocks(file), file)).get(".tag-btn");
    expect(rule, "BBCodeEditor.vue: no .tag-btn").toBeDefined();
    expect(rule?.["border-radius"]).toBe("2px");
  });
});

/**
 * Words that name a picture in a class, read word by word: "logo" is not the
 * start of "logout", nor "cover" the end of "recover". "preview" is left out:
 * on this tree it names a chat in the list of chats, the text of a message and
 * a strip of a hint, and the thumbnail of an upload is a "thumb". A QR code is
 * a picture the page draws, and its white field is the frame that holds it.
 */
const PICTURE_WORDS = new Set([
  "avatar",
  "image",
  "img",
  "picture",
  "photo",
  "thumb",
  "thumbnail",
  "cover",
  "logo",
  "portrait",
  "qr",
]);

/** The elements that are a picture whatever their class says. */
const PICTURE_ELEMENTS = new Set(["img", "picture"]);

/**
 * Words that make a class with a picture word a control that acts on one: the
 * cross of the viewer, a button that loads an avatar. A control keeps the
 * site's radius.
 */
const CONTROL_WORDS = new Set(["btn", "button", "close", "toggle", "input"]);

/** The stylesheets the build compiles, compiled the same way, one by one. */
function compiledSheets(): { path: string; css: string }[] {
  const prelude = new Set(
    [...sassPrelude().matchAll(/@use "@\/([\w/.-]+)"/g)].map(
      (found) => `${found[1]}.sass`,
    ),
  );
  const sheets: { path: string; css: string }[] = [];
  for (const file of styleFiles()) {
    const path = asPath(file);
    const source = readFileSync(file, "utf8");
    if (path.endsWith(".vue")) {
      if (!/<style[^>]*lang="sass"/.test(source)) continue;
      sheets.push({ path, css: compileSass(sassBlocks(file), file) });
    } else if (path.endsWith(".css")) {
      sheets.push({ path, css: source });
    } else if (GLOBAL_SHEETS.has(path) && !prelude.has(path)) {
      // A partial only declares mixins, and they are compiled where they are
      // included; a sheet main.ts imports is where its rules are emitted.
      sheets.push({ path, css: compileSass(source, file) });
    }
  }
  return sheets;
}

/**
 * The element a selector styles: its last compound, with what only wraps or
 * filters it taken away (:deep() unwrapped; :not(), :has(), arguments and
 * attributes dropped, so ":not(img)" is not read as a picture).
 */
function subjectOf(selector: string): string {
  let plain = selector.replace(/:(?:deep|global|slotted)\(([^()]*)\)/g, "$1");
  for (let before = ""; before !== plain; ) {
    before = plain;
    plain = plain.replace(/\([^()]*\)/g, "");
  }
  plain = plain.replace(/\[[^\]]*\]/g, "");
  const compounds = plain.trim().split(/\s*[\s>+~]\s*/);
  return compounds[compounds.length - 1] ?? "";
}

/** Whether a compound selects a picture, or the frame that only holds one. */
function isPicture(compound: string): boolean {
  const element = compound.match(/^([a-z][a-z0-9-]*)/)?.[1];
  if (element && PICTURE_ELEMENTS.has(element)) return true;
  return [...compound.matchAll(/\.([\w-]+)/g)].some(([, name]) => {
    const words = name.toLowerCase().split(/[-_]+/);
    return (
      words.some((word) => PICTURE_WORDS.has(word)) &&
      !words.some((word) => CONTROL_WORDS.has(word))
    );
  });
}

/** A value that leaves the corner square. */
const SQUARE = /^(?:(?:0(?:px|%|em|rem)?\s*)+|initial|unset)$/;

/**
 * The one picture the site rounds: the logos of the partners. They were round
 * until the wave of 5 October squared them with every other picture, and the
 * owner had them round again on 12 October 2026. Exact to the file, the
 * selector and the value, so that no other picture shelters under it.
 */
const ROUND_PICTURE =
  "widgets/sidebar/PartnersPanel.vue: .partner-logo { border-radius: 50% }";

describe("a picture", () => {
  it("is never rounded, nor the frame or the shimmer that stands for it", () => {
    // Owner, 5 October 2026, after round partner logos, avatars of 8 and
    // a picture of a tooltip: never round a picture, anywhere. On 12 October
    // he took the partners back, and only them (ROUND_PICTURE). Read off the
    // compiled sheets, so a radius that arrives through a mixin is caught
    // where it lands: the shimmer of a loading avatar took its 4 from
    // +skeleton-shimmer, and the source of the avatar said nothing.
    const offenders: string[] = [];
    let pictures = 0;
    let roundPicture = false;
    for (const { path, css } of compiledSheets()) {
      for (const { selector, declarations } of styleRules(css)) {
        const rounding = Object.entries(declarations).filter(
          ([property, value]) =>
            (/^border(?:-[a-z]+)*-radius$/.test(property) &&
              !SQUARE.test(value)) ||
            (property === "clip-path" &&
              /\b(?:circle|ellipse|round)\b/.test(value)),
        );
        for (const one of selector.split(",")) {
          if (!isPicture(subjectOf(one))) continue;
          pictures += 1;
          for (const [property, value] of rounding) {
            const found = `${path}: ${one.trim()} { ${property}: ${value} }`;
            if (found === ROUND_PICTURE) roundPicture = true;
            else offenders.push(found);
          }
        }
      }
    }
    expect(offenders).toEqual([]);
    // The exception is in use, so it cannot outlive the round logos.
    expect(roundPicture, ROUND_PICTURE).toBe(true);
    // The scan sees the avatars, thumbnails and pictures of the tree.
    expect(pictures).toBeGreaterThan(30);
  }, 60_000);

  it("is told from a control that acts on one", () => {
    for (const picture of [
      "img",
      ".upload-thumb",
      ".partner-logo",
      ".skeleton-avatar",
      ".bb-image-frame",
      ".avatar-wrapper-unsized",
      ".content :deep(img)",
      ".chain-popover .character-picture:hover",
      ".default-avatar-bg",
    ])
      expect(isPicture(subjectOf(picture)), picture).toBe(true);
    for (const other of [
      ".image-viewer-close",
      ".image-viewer-close::before",
      ".avatar-upload-btn",
      ".logout-dropdown",
      ".recover-link",
      ".messages-container::-webkit-scrollbar-thumb",
      ".chat-preview",
      ".avatar img + .badge",
      "button:not(img)",
      ".gallery > .caption",
    ])
      expect(isPicture(subjectOf(other)), other).toBe(false);
  });
});

describe("a card of content", () => {
  it("is square, the radius being the mark of a control", () => {
    const card = ruleMap(compileSass(".probe\n  +card")).get(".probe");
    expect(card?.border).toBe("1px solid var(--border)");
    expect(card?.["border-radius"]).toBeUndefined();
  });

  it("is not rounded again by the block that takes the recipe", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const lines = readFileSync(file, "utf8").split("\n");
      lines.forEach((line, at) => {
        if (!/^\s*\+card\b/.test(line)) return;
        const indent = line.length - line.trimStart().length;
        // The declarations of the same block, on both sides of the include.
        for (const step of [-1, 1]) {
          for (let i = at + step; i >= 0 && i < lines.length; i += step) {
            const other = lines[i];
            if (!other.trim()) continue;
            const own = other.length - other.trimStart().length;
            if (own < indent) break;
            if (own === indent && /^\s*border-radius:/.test(other))
              offenders.push(`${asPath(file)}:${i + 1}: ${other.trim()}`);
          }
        }
      });
    }
    expect(offenders).toEqual([]);
  });

  it("is square in the shared panels that are not controls", () => {
    for (const [file, selector] of [
      ["shared/ui/SettingsSection/SettingsSection.vue", ".settings-section"],
      ["shared/ui/ErrorState/ErrorState.vue", ".error-state"],
      ["shared/ui/Skeleton/CharacterCardSkeleton.vue", ".skeleton-card"],
      ["shared/ui/Skeleton/ChatPreviewSkeleton.vue", ".skeleton-chat"],
    ]) {
      const rule = ruleMap(componentCss(join(CLIENT_SRC, file))).get(selector);
      expect(rule, `${file} has no ${selector}`).toBeDefined();
      expect(rule?.["border-radius"], file).toBeUndefined();
    }
  });
});

describe("a link", () => {
  const compiled = ruleMap(
    compileSass(
      [
        '@use "@/assets/styles/Inputs" as *',
        ".blue\n  +inline-link-button",
        ".grey\n  +muted-link",
        ".grey-button\n  +muted-link-button",
      ].join("\n"),
    ),
  );

  it("eases its colour at the buttons' tempo, an <a> as a <button>", () => {
    const reset = ruleMap(
      compileSass(
        readFileSync(join(CLIENT_SRC, "assets/styles/Reset.sass"), "utf8"),
        join(CLIENT_SRC, "assets/styles/Reset.sass"),
      ),
    );
    expect(reset.get("a")?.transition).toBe("color 0.15s ease");
    for (const one of [".blue", ".grey", ".grey-button"])
      expect(compiled.get(one)?.transition, one).toBe("color 0.15s ease");
  });

  it("turns $link-hover with the underline, whatever colour it rests in", () => {
    // As every link of DM2 does: a grey one included, which used to turn
    // $link at one place, $link-hover at another and only underline at a
    // third.
    expect(compiled.get(".blue")?.color).toBe("var(--link)");
    expect(compiled.get(".grey")?.color).toBe("var(--text-muted)");
    expect(compiled.get(".grey-button")?.color).toBe("var(--text-muted)");
    for (const one of [".blue", ".grey", ".grey-button"]) {
      expect(compiled.get(`${one}:hover:not(:disabled)`), one).toEqual({
        color: "var(--link-hover)",
        "text-decoration": "underline",
      });
      expect(compiled.get(one)?.["text-decoration"], one).toBe("none");
    }
  });
});

/**
 * Where the client may still cut a text under "…", file by file, with the
 * reason and the task that takes the exception away. Everything else shows its
 * text whole: a name, a title or a caption that does not fit wraps.
 */
const CUT_REGISTER: Record<string, { why: string; owner: string }> = {};

/** A source of the client the cut is looked for in: code and styles, no spec. */
const isCutSource = (name: string): boolean =>
  [".vue", ".sass", ".scss", ".css", ".ts"].some((ext) => name.endsWith(ext)) &&
  !name.endsWith(".spec.ts");

/** Ways a text is cut: a declaration, a style object, a call, a clamp. */
const CUT_WAYS: [string, RegExp][] = [
  ["text-overflow: ellipsis", /text-overflow\s*:\s*ellipsis/],
  ["textOverflow: ellipsis", /textOverflow\s*:\s*["'`]ellipsis/],
  ['setProperty("text-overflow"', /setProperty\(\s*["'`]text-overflow/],
  ["line-clamp", /line-clamp|lineClamp/],
];

/** The cuts a file declares, its comments left out. */
function cutsIn(file: string): string[] {
  const clean = readFileSync(file, "utf8")
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .split("\n")
    .map((one) => one.replace(/(^|[^:"'`])\/\/.*$/, "$1"))
    .join("\n");
  return CUT_WAYS.filter(([, way]) => way.test(clean)).map(([name]) => name);
}

describe("no text cut", () => {
  it("is cut under an ellipsis or clamped to lines nowhere but in the register", () => {
    // The owner, 12 October 2026: a block on a phone is the full version,
    // whole, and nothing is ever cut short. A name ended in "…" in a comment,
    // a caption, a chat and the staff table; it wraps now, and a word longer
    // than the line breaks by the rule of the site.
    const offenders: string[] = [];
    let seen = 0;
    for (const file of collect(CLIENT_SRC, isCutSource)) {
      seen += 1;
      const path = asPath(file);
      if (CUT_REGISTER[path]) continue;
      for (const way of cutsIn(file))
        offenders.push(
          `${path}: ${way} - a text wraps and is not cut; an exception goes into CUT_REGISTER with its reason and the task that removes it`,
        );
    }
    expect(offenders).toEqual([]);
    expect(seen).toBeGreaterThan(500);
  });

  it("keeps no exception for a cut that is gone", () => {
    const stale = Object.keys(CUT_REGISTER).filter(
      (path) => cutsIn(join(CLIENT_SRC, path)).length === 0,
    );
    expect(stale).toEqual([]);
  });

  it("names the reason and the owner of every exception", () => {
    for (const [path, { why, owner }] of Object.entries(CUT_REGISTER)) {
      expect(why.length, path).toBeGreaterThan(20);
      expect(owner.length, path).toBeGreaterThan(2);
    }
  });
});
