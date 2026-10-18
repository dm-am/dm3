/**
 * @vitest-environment jsdom
 */

/**
 * A phone shows the page of a narrow window, drawn smaller, and the reader
 * zooms in: there is no layout of the phone's own (owner, 13 October 2026:
 * "просто не делать мобильную версию, и делать как нормальную ... Если надо,
 * сделай, чтобы пользователям надо было приближать"). The old site did the
 * same by leaving its viewport out.
 *
 * Three files hold the one number together: index.html tells a phone to lay
 * the page out that wide, app/App.vue holds the root of the page no narrower
 * (a narrow window of a desktop scrolls sideways instead of finding a layout
 * of its own), and the sliding column takes most of that width
 * (MobileDrawer.vue). The number itself is the narrowest width whose middle
 * column is as wide as the middle column of a desktop of 1366.
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, describe, expect, it } from "vitest";

const HERE = dirname(fileURLToPath(import.meta.url));
const read = (path: string) => readFileSync(join(HERE, path), "utf8");

const INDEX = read("../index.html");
const SHELL = read("app/App.vue");
const SCALE = read("assets/styles/_Breakpoints.sass");
const VARIABLES = read("assets/styles/Variables.sass");

function number(source: string, pattern: RegExp, what: string): number {
  const found = pattern.exec(source);
  if (!found) throw new Error("не нашел: " + what);
  return Number(found[1]);
}

/** The inline script of index.html that decides the width of the layout. */
const SCRIPT = (() => {
  const found = [...INDEX.matchAll(/<script>([\s\S]*?)<\/script>/g)]
    .map((match) => match[1])
    .filter((body) => body.includes("LAYOUT_WIDTH"));
  expect(found).toHaveLength(1);
  return found[0];
})();

const LAYOUT_WIDTH = number(
  SCRIPT,
  /var LAYOUT_WIDTH = (\d+);/,
  "ширина раскладки",
);
const TABLET_WIDTH = number(
  SCRIPT,
  /var TABLET_WIDTH = (\d+);/,
  "порог планшета",
);
const NARROWEST = number(
  SHELL,
  /\$narrowest-layout:\s*(\d+)px/,
  "самая узкая раскладка",
);
const step = (name: string) =>
  number(SCALE, new RegExp(`\\$${name}:\\s*(\\d+)px`), name);
const SIDEBAR = number(VARIABLES, /\$sidebar-width:\s*(\d+)px/, "колонка");
const GRID_STEP = number(VARIABLES, /\$grid-step:\s*(\d+)px/, "шаг сетки");
const BIG = 8 * GRID_STEP;

/** Runs the script in a document whose screen is the given size. */
function viewportOn(screenWidth: number, screenHeight: number): string {
  document.head.innerHTML =
    '<meta name="viewport" content="width=device-width, initial-scale=1.0">';
  Object.defineProperty(window.screen, "width", {
    value: screenWidth,
    configurable: true,
  });
  Object.defineProperty(window.screen, "height", {
    value: screenHeight,
    configurable: true,
  });
  new Function(SCRIPT)();
  return document
    .querySelector('meta[name="viewport"]')!
    .getAttribute("content")!;
}

afterEach(() => {
  document.head.innerHTML = "";
});

describe("the layout of a phone", () => {
  it("is the narrowest layout of the shell", () => {
    expect(LAYOUT_WIDTH).toBe(NARROWEST);
    // The shell of a narrow window: the columns slide in, one button.
    expect(LAYOUT_WIDTH).toBeLessThanOrEqual(step("bp-shell"));
  });

  it("gives the middle column the width it has on a desktop of 1366", () => {
    // The page's gutter on either side of its text (App.vue, .content).
    expect(SHELL).toMatch(
      /\n\.content\n(?:[ \t]+.*\n)*?[ \t]+padding: 0 \$big\n/,
    );
    const desktop = 1366 - 2 * SIDEBAR - 2 * BIG;
    const phone = LAYOUT_WIDTH - 2 * BIG;
    expect(desktop).toBe(642);
    expect(phone).toBeGreaterThanOrEqual(desktop);
    // And no wider than it has to be: every pixel more is smaller type.
    expect(phone - desktop).toBeLessThan(GRID_STEP);
  });

  it("is laid out on a device narrower than a tablet, whichever way it is turned", () => {
    expect(TABLET_WIDTH).toBe(step("bp-tablet"));
    expect(viewportOn(390, 844)).toBe(`width=${LAYOUT_WIDTH}`);
    expect(viewportOn(844, 390)).toBe(`width=${LAYOUT_WIDTH}`);
    expect(viewportOn(360, 740)).toBe(`width=${LAYOUT_WIDTH}`);
    expect(viewportOn(TABLET_WIDTH - 1, 1024)).toBe(`width=${LAYOUT_WIDTH}`);
  });

  it("leaves a tablet and a desktop the width of the device", () => {
    const own = "width=device-width, initial-scale=1.0";
    expect(viewportOn(TABLET_WIDTH, 1024)).toBe(own);
    expect(viewportOn(1024, TABLET_WIDTH)).toBe(own);
    expect(viewportOn(1920, 1080)).toBe(own);
    // A screen that reports nothing is left alone too.
    expect(viewportOn(0, 0)).toBe(own);
  });

  it("keeps the size of the text the page sets", () => {
    // Left to itself a phone enlarges the paragraphs it takes for too small,
    // and the lines break where the layout did not break them.
    const style = /<style>([\s\S]*?)<\/style>/.exec(INDEX)?.[1] ?? "";
    expect(style).toMatch(/html\s*\{[^}]*-webkit-text-size-adjust:\s*100%;/);
    expect(style).toMatch(/html\s*\{[^}]*[^-]text-size-adjust:\s*100%;/);
  });

  it("gives the sliding column most of the screen and no more than most of the layout", () => {
    const drawer = read("shared/ui/Drawer/MobileDrawer.vue");
    const cap = number(
      drawer,
      /width: min\(85vw, (\d+)px\)/,
      "ширина выдвижной колонки",
    );
    expect(cap).toBeLessThanOrEqual(0.85 * LAYOUT_WIDTH);
    expect(cap).toBeGreaterThan(0.85 * LAYOUT_WIDTH - GRID_STEP);
  });
});
