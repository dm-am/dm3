import { test, expect } from "@playwright/test";
import type { Browser, Page } from "@playwright/test";
import { settle } from "../../fixtures/window";

/**
 * The period row of the site statistics: the segments and the pill side by
 * side; nothing in the row is cut or scrolls, and the row is never wider than
 * its column. September, because its name is the longest of the months.
 */
const SCREENS = [1366, 1920] as const;
type Screen = (typeof SCREENS)[number];

/** A window of a width. */
async function open(browser: Browser, screen: Screen): Promise<Page> {
  const context = await browser.newContext({
    viewport: { width: screen, height: 800 },
  });
  return context.newPage();
}

test.describe("The period row of the site statistics", () => {
  for (const width of SCREENS) {
    test(`shows the segments and the whole pill without scrolling at ${width}px`, async ({
      browser,
    }) => {
      const page = await open(browser, width);
      await page.goto("/statistics?year=2025&month=9");
      const row = page.locator(".site-statistics-page .controls");
      await expect(row.locator(".period-picker")).toBeVisible({
        timeout: 15000,
      });
      await settle(page);

      const m = await row.evaluate((el) => {
        const box = (selector: string) => {
          const r = el.querySelector(selector)!.getBoundingClientRect();
          return { left: r.left, right: r.right, top: r.top, bottom: r.bottom };
        };
        const r = el.getBoundingClientRect();
        return {
          row: { left: r.left, right: r.right },
          segments: box(".period-segments"),
          picker: box(".period-picker"),
          scroll: el.scrollWidth - el.clientWidth,
          page:
            document.documentElement.scrollWidth -
            document.documentElement.clientWidth,
        };
      });

      // Nothing scrolls, and neither part runs out of the row.
      expect(m.scroll).toBeLessThanOrEqual(0);
      expect(m.page).toBeLessThanOrEqual(0);
      for (const part of [m.segments, m.picker]) {
        expect(part.left).toBeGreaterThanOrEqual(m.row.left - 0.5);
        expect(part.right).toBeLessThanOrEqual(m.row.right + 0.5);
      }
      // The segments open the row at its edge.
      expect(Math.abs(m.segments.left - m.row.left)).toBeLessThanOrEqual(0.5);

      expect(m.picker.top).toBe(m.segments.top);
      expect(m.picker.left).toBeGreaterThan(m.segments.right);
      await page.context().close();
    });
  }
});
