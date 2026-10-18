import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import type { Browser, BrowserContext, Page } from "@playwright/test";

/**
 * A phone as the site draws on it: the page of a narrow window laid out at the
 * width index.html gives every device whose shorter side is narrower than a
 * tablet's, and the screen shows that width whole, smaller (owner, 13 October
 * 2026: no layout of the phone's own; the reader zooms in).
 *
 * A phone is emulated as one, with the viewport tag honoured (isMobile), so
 * what is read is what a phone lays out. Firefox has no such emulation and
 * ignores the tag; there the page is opened at the layout width itself, which
 * is what the phone lays out, scaled to the same screen, with a touch screen.
 */

const HERE = dirname(fileURLToPath(import.meta.url));

/** The width a phone lays the page out at, as index.html says it. */
export const LAYOUT_WIDTH = (() => {
  const index = readFileSync(join(HERE, "..", "..", "index.html"), "utf8");
  const found = /var LAYOUT_WIDTH = (\d+);/.exec(index);
  if (!found) throw new Error("index.html no longer says the layout width");
  return Number(found[1]);
})();

/** The phones of the owner's frames, by the width of their screens. */
export const PHONES = [360, 390, 430] as const;

/** The screen of a phone: 390 by 844 unless said otherwise. */
export interface PhoneScreen {
  width?: number;
  height?: number;
}

/**
 * A context of a phone, with the session given or none. The caller closes it.
 */
export async function phoneContext(
  browser: Browser,
  options: PhoneScreen & { storageState?: string } = {},
): Promise<BrowserContext> {
  const width = options.width ?? 390;
  const height = options.height ?? 844;
  const emulated = browser.browserType().name() !== "firefox";
  return browser.newContext({
    storageState: options.storageState,
    ...(emulated
      ? {
          viewport: { width, height },
          deviceScaleFactor: 3,
          isMobile: true,
          hasTouch: true,
        }
      : {
          viewport: {
            width: LAYOUT_WIDTH,
            height: Math.round((height * LAYOUT_WIDTH) / width),
          },
          hasTouch: true,
        }),
  });
}

/** A page of a phone; it closes with its context. */
export async function phonePage(
  browser: Browser,
  options: PhoneScreen & { storageState?: string } = {},
): Promise<Page> {
  const context = await phoneContext(browser, options);
  return context.newPage();
}

/**
 * What stands past the right edge of the layout, or makes something scroll
 * sideways, as sentences: the page of a phone is the layout and no wider, and
 * nothing in it is wider than its box. Read in the page, so it is one
 * function with no outside names.
 */
export function pastTheLayout(layoutWidth: number): string[] {
  const problems: string[] = [];
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}${[...el.classList].map((c) => `.${c}`).join("")}`;
  if (window.innerWidth !== layoutWidth)
    problems.push(
      `the layout is ${window.innerWidth} wide, not ${layoutWidth}`,
    );
  for (const scroller of [
    document.scrollingElement,
    document.querySelector(".main"),
  ]) {
    if (scroller && scroller.scrollWidth > scroller.clientWidth + 1)
      problems.push(
        `${name(scroller)} scrolls sideways: ${scroller.scrollWidth} > ${scroller.clientWidth}`,
      );
  }
  for (const el of document.querySelectorAll("#app *")) {
    if (el.closest("svg, [aria-hidden='true'], .drawer-panel, .skip-link"))
      continue;
    if (!el.getClientRects().length) continue;
    const style = getComputedStyle(el);
    if (style.position === "fixed") continue;
    const box = el.getBoundingClientRect();
    if (box.width > 0 && box.right > layoutWidth + 1)
      problems.push(`${name(el)} reaches ${Math.round(box.right)}`);
  }
  return problems;
}
