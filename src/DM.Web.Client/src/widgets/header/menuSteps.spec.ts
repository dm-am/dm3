/**
 * @vitest-environment node
 */

/**
 * The top menu of the header stands between the logo and the statistics at
 * every desktop width, from the left of the room they leave it, as the menu of
 * DM2 starts right after its logo, and it wraps into a second line of the
 * same size where one line does not hold it. The owner on 13 October 2026, on
 * the menu centred and stepped down to 17 on a laptop: "верни по левому краю.
 * И зачем ты уменьшаешь размер? Там есть место под этот размер в две строки."
 *
 * The side columns of the header are fixed at $sidebar-width each, so only the
 * menu can give up room, and it does so in font steps (Header.vue). The rule
 * kept here:
 *
 * 1. Every step but the smallest holds its links in two lines at most across
 *    its whole range, for eight links and for nine, with the scrollbar drawn.
 * 2. No step comes on sooner than it must: one width wider than where a step
 *    starts, the step above it would take a third line. Where two lines of
 *    the full size fit, the full size stands; 1366 has it.
 * 3. Where not even the smallest step holds two lines, it takes a third, never
 *    breaking a link, and the lines stay inside the header.
 * 4. The menu starts on the line the text of the page starts on, and its lines
 *    stand in the middle of the header, as the two columns beside it centre
 *    their own contents. In the smallest step it gives half of its sides to
 *    the links, so that two lines hold eight of them down to the breakpoint.
 * 5. No width of a desktop moves the menu out of its place between the two.
 *
 * Nothing in jsdom lays out a line, so the links are measured from the face
 * the site ships (shared/lib/testing/fontFace.ts), the tracking of the links
 * included, and the steps are read out of the compiled stylesheet of the
 * header, the cascade applied by weight and order. The model is calibrated
 * against Chrome, and a canary proves it can fail: nine links on the steps
 * sized for eight have to run short of room.
 *
 * What this file cannot see: the frame before the web font arrives, and a
 * larger size of type set in the browser, which grows the links while every
 * width in the sheet stays put.
 */
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { visibleMainNavSections } from "@/shared/config/mainNavigation";
import { loadFace } from "@/shared/lib/testing/fontFace";
import {
  componentCss,
  outranks,
  specificity,
  styleRules,
  type Declarations,
} from "@/shared/lib/testing/styles";

const HERE = dirname(fileURLToPath(import.meta.url));
const SRC = resolve(HERE, "../..");
const read = (path: string) => readFileSync(resolve(SRC, path), "utf8");

function px(source: string, pattern: RegExp, what: string): number {
  const found = source.match(pattern);
  if (!found) throw new Error("не нашел в листе: " + what);
  return Number(found[1]);
}

const SIDEBAR = px(
  read("assets/styles/Variables.sass"),
  /\$sidebar-width:\s*(\d+)px/,
  "ширина боковой колонки",
);
const HEADER_HEIGHT = px(
  read("assets/styles/Layout.sass"),
  /\$header-height:\s*(\d+)px/,
  "высота шапки",
);
const BP_SHELL = px(
  read("assets/styles/_Breakpoints.sass"),
  /\$bp-shell:\s*(\d+)px/,
  "порог мобильной шапки",
);
/** The line-height every link inherits from the body. */
const LINE_HEIGHT = px(
  read("assets/styles/Reset.sass"),
  /\nbody\n(?:[ \t]+.*\n)*?[ \t]+line-height:\s*([\d.]+)/,
  "межстрочный интервал страницы",
);

/**
 * Chrome's classic scrollbar on Windows. The page scrolls in .main, which
 * always draws it (overflow-y: scroll), so a window of 1024px leaves the page
 * 1009px: measured in headless Chrome on 24 September 2026. Overlay scrollbars
 * take nothing, so a menu that fits beside a drawn one fits everywhere.
 */
const SCROLLBAR = 15;
/** The widest window the rule is swept over: past it the menu only gains. */
const WIDEST = 1920;
/**
 * Chrome lays a link out in 1/64 px and runs up to 0.02px past the face's own
 * advances per link, so a line is judged with one pixel to spare.
 */
const SLACK = 1;

// ───────────────────────────────────────────────────────── the stylesheet ──

interface Rule {
  /** The widest window the rule applies in: a @media (max-width) or none. */
  upTo: number;
  /** Some other condition of a @media: the rule is not a step. */
  other: boolean;
  selector: string;
  declarations: Declarations;
}

/** The compiled sheet as rules in source order, each @media opened. */
function rulesOf(css: string): Rule[] {
  const out: Rule[] = [];
  let depth = 0;
  let start = 0;
  let bodyStart = 0;
  let head = "";
  for (let at = 0; at < css.length; at += 1) {
    if (css[at] === "{") {
      if (depth === 0) {
        head = css.slice(start, at).split(";").pop()!.trim();
        bodyStart = at + 1;
      }
      depth += 1;
    } else if (css[at] === "}") {
      depth -= 1;
      if (depth > 0) continue;
      const body = css.slice(bodyStart, at);
      if (!head.startsWith("@")) {
        for (const rule of styleRules(`${head}{${body}}`))
          out.push({ upTo: Infinity, other: false, ...rule });
      } else if (head.startsWith("@media")) {
        const step = head.match(/^@media \(max-width: (\d+)px\)$/);
        for (const rule of styleRules(body))
          out.push({
            upTo: step ? Number(step[1]) : Infinity,
            other: !step,
            ...rule,
          });
      }
      start = at + 1;
    }
  }
  return out;
}

const RULES = rulesOf(componentCss(resolve(HERE, "Header.vue")));
/** The menu itself: the look of its links and how its lines run. */
const MENU_RULES = rulesOf(componentCss(resolve(HERE, "MainMenu.vue")));

/** A condition on the number of links: the nine-link steps are written so. */
const COUNTED = /:has\(\.top-menu > \.link:nth-child\((\d+)\)\)/g;

/** The class of a header that is not a desktop's. */
const NOT_DESKTOP = /\.header--narrow\b/;

/**
 * Whether a selector reaches the header when the menu holds that many links.
 * With `counted` off, a selector conditioned on the number of links reaches
 * nothing: the sheet as it would stand if nine had no steps of their own.
 */
function reachesHeader(
  selector: string,
  links: number,
  counted: boolean,
): boolean {
  let holds = true;
  const plain = selector.replace(COUNTED, (_, nth: string) => {
    if (!counted || links < Number(nth)) holds = false;
    return "";
  });
  return holds && plain === ".header";
}

/**
 * The value the cascade gives a custom property of the header in one window:
 * the heaviest rule that reaches it, the later of two equal ones.
 */
function stepValue(
  window: number,
  links: number,
  property: string,
  counted: boolean,
): number {
  let best: { value: string; weight: [number, number, number] } | undefined;
  for (const rule of RULES) {
    if (rule.other || window > rule.upTo) continue;
    for (const selector of rule.selector.split(",").map((s) => s.trim())) {
      if (!reachesHeader(selector, links, counted)) continue;
      const value = rule.declarations[property];
      if (value === undefined) continue;
      const weight = specificity(selector);
      if (!best || !outranks(best.weight, weight)) best = { value, weight };
    }
  }
  if (!best) throw new Error(`не нашел в листе: ${property}`);
  return parseFloat(best.value);
}

interface Step {
  font: number;
  gap: number;
  /** The menu's own two sides, which the links do not have. */
  left: number;
  right: number;
}

/**
 * The menu's two sides, left and right, as its padding writes them: a length,
 * or a custom property the steps set.
 */
const MENU_SIDES = (() => {
  const paddings = desktopValues(".top-menu", "padding");
  expect(paddings).toHaveLength(1);
  const [, right, , left] = sides(paddings[0]);
  return { left, right };
})();

const STEP_CACHE = new Map<string, Step>();

/** The step of the menu in one window, remembered per window. */
function stepAt(window: number, links: number, counted = true): Step {
  const key = `${window}/${links}/${counted}`;
  const known = STEP_CACHE.get(key);
  if (known) return known;
  const side = (value: string) => {
    const custom = /^var\((--[\w-]+)\)$/.exec(value);
    return custom
      ? stepValue(window, links, custom[1], counted)
      : parseFloat(value);
  };
  const step = {
    font: stepValue(window, links, "--menu-font", counted),
    gap: stepValue(window, links, "--menu-gap", counted),
    left: side(MENU_SIDES.left),
    right: side(MENU_SIDES.right),
  };
  STEP_CACHE.set(key, step);
  return step;
}

/** The declarations of the one rule a sheet writes for a selector. */
function declared(selector: string, rules = RULES): Declarations {
  const found = rules.filter((rule) => rule.selector === selector);
  expect(found, selector).toHaveLength(1);
  return found[0].declarations;
}

/**
 * Every value a desktop header can give a property of one of its boxes: the
 * rules whose selector ends in the box and names no narrow header's class, in
 * any window.
 */
function desktopValues(box: string, property: string): string[] {
  const values: string[] = [];
  for (const rule of RULES) {
    const reaches = rule.selector
      .split(",")
      .map((s) => s.trim())
      .some((s) => s.endsWith(box) && !NOT_DESKTOP.test(s));
    const value = rule.declarations[property];
    if (reaches && value !== undefined) values.push(value);
  }
  return values;
}

/** A padding shorthand opened into its four sides. */
function sides(padding: string): [string, string, string, string] {
  const [t, r = t, b = t, l = r] = padding.split(/\s+/);
  return [t, r, b, l];
}

// ───────────────────────────────────────────────────────────── the model ──

/** The tracking of the links (MainMenu.vue), which lands per character. */
const TRACKING = parseFloat(declared(".link", MENU_RULES)["letter-spacing"]);

const face = loadFace(resolve(SRC, "assets/fonts/PTSans-Regular.ttf"));
const linkWidth = (title: string, size: number) =>
  face.width(title, size, TRACKING);

const MENUS = {
  player: visibleMainNavSections(false).map((s) => s.title),
  moderator: visibleMainNavSections(true).map((s) => s.title),
};

/** The page is the window less the scrollbar. */
const page = (window: number) => window - SCROLLBAR;

/** The room between the logo and the statistics in one window. */
const menuRoom = (window: number) => page(window) - 2 * SIDEBAR;

/** What the links have of a line: the room less the menu's own two sides. */
const linksRoom = (window: number, step: Step) =>
  menuRoom(window) - step.left - step.right - SLACK;

/** How many lines a wrapping flex row breaks the links into. */
function lines(titles: string[], step: Step, width: number): number {
  let count = 1;
  let used = 0;
  for (const title of titles) {
    const next = linkWidth(title, step.font);
    if (used > 0 && used + step.gap + next > width) {
      count += 1;
      used = next;
    } else {
      used += (used > 0 ? step.gap : 0) + next;
    }
  }
  return count;
}

/** The lines of the menu in one window, at the step the sheet gives it. */
function linesAt(titles: string[], window: number, counted = true): number {
  const step = stepAt(window, titles.length, counted);
  return lines(titles, step, linksRoom(window, step));
}

const DESKTOP = Array.from(
  { length: WIDEST - BP_SHELL },
  (_, i) => BP_SHELL + 1 + i,
);

/** Consecutive windows folded into ranges, the way a failure is read. */
function ranges(windows: number[]): string {
  const out: [number, number][] = [];
  for (const w of windows) {
    const last = out[out.length - 1];
    if (last && last[1] === w - 1) last[1] = w;
    else out.push([w, w]);
  }
  return out.map(([a, b]) => (a === b ? `${a}` : `${a}-${b}`)).join(", ");
}

/** The steps as "size from width", widest first. */
function steps(links: number): string {
  const out: string[] = [];
  let font = NaN;
  for (let w = WIDEST; w > BP_SHELL; w -= 1) {
    const size = stepAt(w, links).font;
    if (size !== font) out.push(`${size}px`);
    else out[out.length - 1] = `${size}px from ${w}`;
    font = size;
  }
  out[out.length - 1] = out[out.length - 1].replace(/ from \d+$/, " below");
  return out.join(", ");
}

/** The sizes of the steps, largest first. */
const SIZES = [...new Set(DESKTOP.map((w) => stepAt(w, 9).font))].sort(
  (a, b) => b - a,
);
const SMALLEST = SIZES[SIZES.length - 1];

/** The step of the sheet one size up from a size, with its own gap. */
function stepAbove(size: number, links: number): Step | null {
  const larger = SIZES.filter((s) => s > size);
  if (!larger.length) return null;
  const font = Math.min(...larger);
  const window = DESKTOP.find((w) => stepAt(w, links).font === font)!;
  return stepAt(window, links);
}

/** The windows where a step above the smallest runs past two lines. */
function shortOfRoom(titles: string[], counted: boolean): number[] {
  return DESKTOP.filter((w) => {
    const step = stepAt(w, titles.length, counted);
    return step.font > SMALLEST && linesAt(titles, w, counted) > 2;
  });
}

// ────────────────────────────────────────────────────────────── the rule ──

describe("the top menu of the header between the logo and the statistics", () => {
  it("measures the links the way Chrome does", () => {
    // The nine links of a moderator on one line, in each step, with their
    // tracking of 1px: measured in headless Chrome on 14 October 2026 in the
    // shipped face.
    const chrome: Record<number, number> = {
      25: 905.72,
      20: 737.39,
      17: 636.41,
      15: 569.05,
    };
    expect(TRACKING).toBe(1);
    for (const [size, width] of Object.entries(chrome)) {
      const model = MENUS.moderator.reduce(
        (sum, title) => sum + linkWidth(title, Number(size)),
        0,
      );
      expect(Math.abs(model - width), `${size}px`).toBeLessThan(SLACK / 10);
    }
    expect(MENUS.player).toHaveLength(8);
    expect(MENUS.moderator).toHaveLength(9);
  });

  it("catches nine links on the steps sized for eight", () => {
    // The canary: the sheet read as if the nine-link steps were not there must
    // run past two lines in every step above the smallest, or a green run
    // below would not mean the model can see the defect.
    const short = shortOfRoom(MENUS.moderator, false);
    const fonts = new Set(short.map((w) => stepAt(w, 9, false).font));
    expect([...fonts].sort((a, b) => a - b)).toEqual([17, 20, 25]);
  });

  it("takes each step at the width it is set to", () => {
    // Pinned, because the widths are a visual decision and not only a fit: a
    // step that comes on sooner shrinks the menu in windows where it fitted.
    expect(steps(8)).toBe(
      "25px from 1260, 20px from 1134, 17px from 1068, 15px below",
    );
    expect(steps(9)).toBe(
      "25px from 1358, 20px from 1203, 17px from 1125, 15px below",
    );
    // The full size on the laptop of 1366, for a moderator too.
    expect(stepAt(1366, 8).font).toBe(25);
    expect(stepAt(1366, 9).font).toBe(25);
  });

  it("holds every step above the smallest in two lines at most", () => {
    for (const [who, titles] of Object.entries(MENUS)) {
      expect(ranges(shortOfRoom(titles, true)), who).toBe("");
    }
  });

  it("steps down only where two lines of the size above no longer hold it", () => {
    for (const [who, titles] of Object.entries(MENUS)) {
      const tooSoon = DESKTOP.filter((w) => {
        const above = stepAbove(stepAt(w, titles.length).font, titles.length);
        return above !== null && lines(titles, above, linksRoom(w, above)) <= 2;
      });
      expect(ranges(tooSoon), who).toBe("");
    }
  });

  it("takes a third line in the smallest step only where two do not hold it, inside the header", () => {
    // The menu may wrap, and a link may not: the wrap falls between links.
    expect(declared(".main-menu", MENU_RULES)["flex-wrap"]).toBe("wrap");
    expect(declared(".link", MENU_RULES)["white-space"]).toBe("nowrap");

    const baseline = parseFloat(
      sides(desktopValues(".top-menu", "padding")[0])[2],
    );
    const found: string[] = [];
    for (const [who, titles] of Object.entries(MENUS)) {
      const bad: number[] = [];
      const third: number[] = [];
      for (const w of DESKTOP) {
        const step = stepAt(w, titles.length);
        const width = linksRoom(w, step);
        const longest = Math.max(...titles.map((t) => linkWidth(t, step.font)));
        const count = lines(titles, step, width);
        if (count > 2) third.push(w);
        const height = count * LINE_HEIGHT * step.font;
        if (
          longest > width ||
          count > 3 ||
          (count > 2 && step.font !== SMALLEST) ||
          height > HEADER_HEIGHT - baseline
        )
          bad.push(w);
      }
      expect(ranges(bad), who).toBe("");
      found.push(`${who}: ${ranges(third) || "none"}`);
    }
    // Pinned so a change of the menu or of the steps is seen.
    expect(found).toEqual(["player: none", "moderator: 1001-1047"]);
  });

  it("starts on the line of the page's text and stands in the middle of the header", () => {
    // Across: from the left of its room, every line, as on DM2. The room
    // starts where the left column of the page ends, and the menu's own side
    // is the gutter the text of the page stands in, so the first link stands
    // over the first letter of the page. The smallest step gives half of the
    // gutter on either side to the links: that is what holds eight of them in
    // two lines right down to the breakpoint.
    expect(declared(".main-menu", MENU_RULES)["justify-content"]).toBe(
      "flex-start",
    );
    expect(desktopValues(".top-menu", "justify-content")).toEqual([]);
    const content = styleRules(componentCss(resolve(SRC, "app/App.vue"))).find(
      (rule) => rule.selector === ".content",
    )!.declarations;
    const gutter = parseFloat(sides(content.padding)[3]);
    for (const [who, titles] of Object.entries(MENUS)) {
      const off = DESKTOP.filter((w) => {
        const step = stepAt(w, titles.length);
        const side = step.font === SMALLEST ? gutter / 2 : gutter;
        return step.left !== side || step.right !== side;
      });
      expect(ranges(off), who).toBe("");
    }

    // Down: the lines, one, two or three, are centred as one block in the
    // height the columns beside it centre their own contents in: the header
    // less the same baseline padding.
    expect(desktopValues(".top-menu", "align-content")).toEqual(["center"]);
    expect(declared(".main-menu", MENU_RULES)["align-items"]).toBe("center");
    const menuPad = sides(desktopValues(".top-menu", "padding")[0])[2];
    expect(desktopValues(".user-info", "padding-bottom")).toEqual([menuPad]);
    expect(desktopValues(".stats-col", "padding-bottom")).toEqual([menuPad]);
    expect(desktopValues(".user-info", "justify-content")).toEqual(["center"]);
    expect(desktopValues(".stats-col", "align-items")).toEqual(["center"]);

    // The menu's room is the middle column of the header, after a column as
    // wide as the left column of the page.
    const header = RULES.find(
      (rule) =>
        rule.selector === ".header" &&
        rule.declarations["grid-template-columns"],
    )!.declarations;
    const [left, middle, right] =
      header["grid-template-columns"].split(/\s+(?![^(]*\))/);
    expect(left).toBe(`${SIDEBAR}px`);
    expect(middle).toBe("minmax(0, 1fr)");
    expect(right).toBe(`${SIDEBAR}px`);
    expect(header["grid-template-areas"]).toBe('"user menu stats"');
  });

  it("is laid out by the class of the header, never by a width of a desktop", () => {
    // A @media of the header steps the type of the menu and nothing else.
    const laidOut = RULES.filter(
      (rule) =>
        rule.upTo !== Infinity &&
        Object.keys(rule.declarations).some(
          (name) => !name.startsWith("--menu-"),
        ),
    ).map((rule) => `${rule.selector} up to ${rule.upTo}`);
    expect(laidOut).toEqual([]);
  });
});
