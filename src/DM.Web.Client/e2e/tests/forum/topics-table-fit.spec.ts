import {
  test as base,
  expect,
  PRIMARY_STORAGE_STATE,
} from "../../fixtures/auth";
import { readTables } from "../../fixtures/tables";
import { settle, windowContext } from "../../fixtures/window";
import type { Page } from "@playwright/test";

/**
 * The topics table keeps every column and every word of what it shows whole
 * at every width of the full version, and never widens the page.
 *
 * The table lays out "auto": the px widths are where the columns start from,
 * and where even its least width, every word whole, is wider than the column,
 * the table keeps that width and scrolls sideways in its own frame, and only
 * there (fixtures/tables.ts). Its frame keeps to the column.
 *
 * Read as a guest and as the primary seeded account, an administrator: that
 * viewer may pin a topic on every board, with a pin before every title.
 */
/** A window as a guest and as the administrator, each in a context of its own. */
const test = base.extend<{ guest: Page; moderator: Page }>({
  guest: async ({ browser }, use) => {
    const context = await windowContext(browser, { width: 1366 });
    await use(await context.newPage());
    await context.close();
  },
  moderator: async ({ browser }, use) => {
    const context = await windowContext(browser, {
      width: 1366,
      storageState: PRIMARY_STORAGE_STATE,
    });
    await use(await context.newPage());
    await context.close();
  },
});

const BOARD = "/forum/general";
const WIDTHS = [1366, 1440, 1920];

const COLUMNS = [
  "Топик",
  "Автор",
  "Комментарии",
  "Нравится",
  "Дата создания",
  "Последняя активность",
];

interface Fit {
  /** Every header of the table. */
  headers: string[];
  widths: Record<string, number>;
  problems: string[];
}

/**
 * The table as it stands: its headers, the widths of its columns, and what is
 * wrong with it, the columns, the words, the frame and the headers read by
 * fixtures/tables.ts. The text under visibility: hidden is read as well: it
 * takes the width of its column like the text seen, and the widths of the
 * columns are what this spec holds.
 */
async function measure(page: Page): Promise<Fit> {
  const tables = await readTables(page, {
    within: ".topics-page",
    words: true,
    heads: true,
    withHidden: true,
  });
  const fit = await measureInPage(page);
  return { ...fit, problems: [...tables.problems, ...fit.problems] };
}

/**
 * What the fixture does not read: the page stays put, the frame keeps to the
 * column, and the widths of the columns.
 */
function measureInPage(page: Page): Promise<Fit> {
  return page.evaluate(() => {
    const problems: string[] = [];
    const scroller = document.querySelector<HTMLElement>(".main")!;
    const column = document.querySelector<HTMLElement>(".topics-page")!;
    const table = column.querySelector<HTMLElement>("table.data-table")!;
    const label = (th: HTMLElement) =>
      (th.textContent ?? "").trim().replace(/\s+/g, " ");
    const shown = (el: Element) => el.getClientRects().length > 0;

    if (scroller.scrollWidth > scroller.clientWidth + 1) {
      problems.push(
        `the page scrolls sideways: ${scroller.scrollWidth} > ${scroller.clientWidth}`,
      );
    }
    // The frame of the table keeps to the column; whether it may scroll is
    // the fixture's to read.
    const frame = table.parentElement!;
    const edge = column.getBoundingClientRect();
    const inner = frame.getBoundingClientRect();
    if (inner.left < edge.left - 0.5 || inner.right > edge.right + 0.5) {
      problems.push(
        `the frame of the table spans ${Math.round(inner.left)}-${Math.round(inner.right)}, the column ${Math.round(edge.left)}-${Math.round(edge.right)}`,
      );
    }
    for (let up = frame.parentElement; up && up !== column; ) {
      if (up.scrollWidth > up.clientWidth + 1) {
        problems.push(`${up.className} scrolls sideways`);
      }
      up = up.parentElement;
    }

    const ths = [...table.querySelectorAll<HTMLElement>("thead th")];
    const widths: Record<string, number> = {};
    for (const th of ths.filter(shown)) {
      const width = th.getBoundingClientRect().width;
      widths[label(th)] = Math.round(width);
      if (width < 1) problems.push(`the column "${label(th)}" has no width`);
    }
    return {
      headers: ths.map(label),
      widths,
      problems,
    };
  });
}

async function open(page: Page, width: number) {
  await page.setViewportSize({ width, height: 900 });
  await page.goto(BOARD);
  await expect(page.locator("td.col-title a.topic-link").first()).toBeVisible({
    timeout: 10000,
  });
  await settle(page);
}

test.describe("Forum topics table width", () => {
  for (const width of WIDTHS) {
    test(`keeps its columns and its words at ${width}px, as a guest`, async ({
      guest: page,
    }) => {
      await open(page, width);

      const fit = await measure(page);
      expect(fit.headers).toEqual(COLUMNS);
      expect(fit.problems).toEqual([]);
      // No handle of the order for a guest: the pin is the mark in the title.
      await expect(page.locator("[data-pin-handle]")).toHaveCount(0);
    });

    test(`keeps its columns and its words at ${width}px, as a moderator`, async ({
      moderator: page,
    }) => {
      await open(page, width);

      const fit = await measure(page);
      // The pins stand in the title cell: no column of their own.
      expect(fit.headers).toEqual(COLUMNS);
      expect(fit.problems).toEqual([]);
      // A pin before every title, pressed where the topic is pinned. The
      // seed pins two topics in this board, so their pins are the handles of
      // the order as well, and no control of the order is added.
      const rows = await page.locator(".topics-page tr.table-row").count();
      await expect(
        page.locator(".topics-page td.col-title button.pin-toggle"),
      ).toHaveCount(rows);
      await expect(
        page.locator('.topics-page .pin-toggle[aria-pressed="true"]'),
      ).toHaveCount(2);
      await expect(page.locator("[data-pin-handle]")).toHaveCount(2);
      await expect(
        page.getByRole("button", {
          name: /порядок закрепленных|сохранить порядок/i,
        }),
      ).toHaveCount(0);
    });

    // On this board the widest title is a pinned one, and the guest has the
    // pin before its first word too, in the link: the pale pins of the
    // moderator widen nothing, and the table is the guest's to the pixel,
    // with the same columns at the same widths.
    test(`is as wide for a moderator as for a guest at ${width}px`, async ({
      guest: page,
      moderator: authenticatedPage,
    }) => {
      await open(page, width);
      await open(authenticatedPage, width);

      const tableWidth = (viewer: Page) =>
        viewer
          .locator(".topics-page table.data-table")
          .evaluate((table) => Math.round(table.getBoundingClientRect().width));
      expect(await tableWidth(authenticatedPage)).toBe(await tableWidth(page));
      expect((await measure(authenticatedPage)).widths).toEqual(
        (await measure(page)).widths,
      );
    });
  }

  // The same page through every width in turn, as a window is widened: the
  // table follows the column, and every column stays at each width.
  test(`keeps every column as the window widens through ${WIDTHS.join(", ")}px`, async ({
    guest: page,
  }) => {
    await open(page, WIDTHS[0]);
    const problems: string[] = [];
    for (const width of WIDTHS) {
      await page.setViewportSize({ width, height: 900 });
      await settle(page);
      const fit = await measure(page);
      problems.push(...fit.problems.map((one) => `${width}px: ${one}`));
      if (fit.headers.join() !== COLUMNS.join())
        problems.push(`${width}px: shows ${fit.headers.join(", ")}`);
    }
    expect(problems).toEqual([]);
  });

  // Where the column has the room, every column stands at its own px width
  // (plus the 24px of its padding), the frame does not scroll, and the
  // headers and the date of creation keep to one line. The last activity
  // writes a name before its date and takes the lines it needs.
  test("stands every column at its own width at 1920px", async ({
    guest: page,
  }) => {
    await open(page, 1920);

    const fit = await measure(page);
    expect(fit.headers).toEqual(COLUMNS);
    expect(fit.problems).toEqual([]);
    expect(
      await page
        .locator(".topics-page .data-table-scroll")
        .evaluate((frame) => frame.scrollWidth - frame.clientWidth),
    ).toBeLessThanOrEqual(1);
    expect(fit.widths).toMatchObject({
      Автор: 204,
      Комментарии: 124,
      Нравится: 92,
      "Дата создания": 158,
      "Последняя активность": 184,
    });
    const lines = await page.evaluate(() =>
      [
        ...document.querySelectorAll<HTMLElement>(
          ".topics-page td.col-created, .topics-page th.col-created, .topics-page th.col-lastActivity",
        ),
      ].map((cell) => {
        const range = document.createRange();
        range.selectNodeContents(cell);
        return new Set(
          [...range.getClientRects()]
            .filter((r) => r.width > 0.5)
            .map((r) => Math.round(r.top)),
        ).size;
      }),
    );
    expect(lines.every((count) => count === 1)).toBe(true);
  });
});
