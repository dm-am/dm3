import { expect } from "../../fixtures/auth";
import { test } from "../../fixtures/roles";
import { settle } from "../../fixtures/window";

/**
 * The menu of the header (UI_STANDARDS, "Шапка"): it stands from the left
 * between the logo and the statistics, on the line the text of the page
 * starts on, in one size, and wraps into a second line at most. The header
 * stays 100, its lines level with the statistics. The administrator has the
 * ninth link.
 */
const DESKTOP_WIDTHS = [1920, 1366];

test.describe("The menu of the header", () => {
  for (const role of ["user", "admin"] as const) {
    for (const width of DESKTOP_WIDTHS) {
      test(`stands from the left between the logo and the statistics as ${role} at ${width}px`, async ({
        openAs,
      }) => {
        const page = await openAs(role, width, {
          path: "/",
          roles: [role],
          ready: "",
        });
        await expect(
          page.locator("header nav.top-menu .link").first(),
        ).toBeVisible();
        await expect(page.locator("header .site-stats")).toBeVisible({
          timeout: 15000,
        });
        await settle(page);
        const fit = await page.evaluate(() => {
          const box = (selector: string) =>
            document.querySelector(selector)!.getBoundingClientRect();
          const links = [
            ...document.querySelectorAll("header nav.top-menu .link"),
          ].map((link) => link.getBoundingClientRect());
          const lines = new Map<number, DOMRect[]>();
          for (const link of links) {
            const top = Math.round(link.top);
            lines.set(top, [...(lines.get(top) ?? []), link]);
          }
          const main = document.querySelector<HTMLElement>("main#main")!;
          const stats = box("header .site-stats");
          const scroller = document.scrollingElement!;
          return {
            header: box("header.header").height,
            logoColumn: box("header .user-info").right,
            statsColumn: box("header .stats-col").left,
            // Where the text of the page starts under the menu.
            text:
              main.getBoundingClientRect().left +
              parseFloat(getComputedStyle(main).paddingLeft),
            starts: [...lines.values()].map((line) =>
              Math.min(...line.map((b) => b.left)),
            ),
            right: Math.max(...links.map((b) => b.right)),
            lines: lines.size,
            size: parseFloat(
              getComputedStyle(
                document.querySelector("header nav.top-menu .link")!,
              ).fontSize,
            ),
            menuMiddle:
              (Math.min(...links.map((b) => b.top)) +
                Math.max(...links.map((b) => b.bottom))) /
              2,
            statsMiddle: (stats.top + stats.bottom) / 2,
            overflow: scroller.scrollWidth - scroller.clientWidth,
          };
        });
        expect(fit.header).toBe(100);
        // Every line starts where the text of the page starts.
        expect(fit.starts.map((start) => Math.round(start - fit.text))).toEqual(
          fit.starts.map(() => 0),
        );
        expect(fit.starts[0]).toBeGreaterThanOrEqual(fit.logoColumn);
        expect(fit.right).toBeLessThanOrEqual(fit.statsColumn);
        expect(fit.lines).toBeLessThanOrEqual(2);
        expect(fit.size).toBe(25);
        expect(Math.abs(fit.menuMiddle - fit.statsMiddle)).toBeLessThanOrEqual(
          2,
        );
        expect(fit.overflow).toBeLessThanOrEqual(1);
      });
    }
  }
});
