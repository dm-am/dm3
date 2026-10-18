import { test, expect } from "@playwright/test";
import type { Browser, Page } from "@playwright/test";
import { phonePage } from "../../fixtures/phone";

/**
 * The period row of the site statistics ("Месяц | Год | Все время" and the
 * pill "‹ Сентябрь 2026 ›") as it stood before 5 October 2026, less the
 * sideways scroll it had on a phone. Where the segments and the pill do not
 * fit side by side, the pill goes down a line of its own, level with the
 * segments over it; nothing in the row is cut, nothing scrolls, and the row is
 * never wider than its column.
 *
 * A phone lays the page out at the width of a narrow window (fixtures/phone.ts)
 * and shows the row as 1366 does; the narrowest column is the one of 1024,
 * between the two sidebars.
 *
 * September, because its name is the longest of the months and the pill is as
 * wide as it at every month.
 */
const SCREENS = [1366, 1024, 768, "phone"] as const;
type Screen = (typeof SCREENS)[number];

/** Where the row lays the pill: beside the segments, or under them. */
const ONE_LINE = new Set<Screen>([1366, 768, "phone"]);
const TWO_LINES = new Set<Screen>([1024]);

/** A phone, or a window of a width. */
async function open(browser: Browser, screen: Screen): Promise<Page> {
  if (screen === "phone") return phonePage(browser);
  const context = await browser.newContext({
    viewport: { width: screen, height: 800 },
  });
  return context.newPage();
}

test.describe("The period row of the site statistics", () => {
  for (const width of SCREENS) {
    const where = width === "phone" ? "on a phone" : `at ${width}px`;
    test(`shows the segments and the whole pill without scrolling ${where}`, async ({
      browser,
    }) => {
      const page = await open(browser, width);
      await page.goto("/statistics?year=2025&month=9");
      const row = page.locator(".site-statistics-page .controls");
      await expect(row.locator(".period-picker")).toBeVisible({
        timeout: 15000,
      });
      await page.evaluate(() => document.fonts.ready);

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

      if (ONE_LINE.has(width)) {
        expect(m.picker.top).toBe(m.segments.top);
        expect(m.picker.left).toBeGreaterThan(m.segments.right);
      }
      if (TWO_LINES.has(width)) {
        // Under the segments, a step below them and level with them.
        expect(m.picker.top - m.segments.bottom).toBe(8);
        expect(Math.abs(m.picker.left - m.segments.left)).toBeLessThanOrEqual(
          0.5,
        );
      }
      await page.context().close();
    });
  }
});
