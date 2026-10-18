import {
  test as base,
  expect,
  PRIMARY_STORAGE_STATE,
} from "../../fixtures/auth";
import { readTables } from "../../fixtures/tables";
import type { Page } from "@playwright/test";

/**
 * The topics table keeps every column and every word of what it shows whole
 * at every width of the full version, and never widens the page.
 *
 * Its columns have px widths that add up to 740px besides "Топик", against a
 * column of 300px at 1024, 556px at 1280, 642px at 1366 and 716px at 1440. It
 * carried a floor of 950px and scrolled sideways inside the column; then its
 * author and date of creation folded into a line under the title below
 * 1100px, then its words broke inside to fit, and then its rarer columns went,
 * all taken back. The table lays out "auto": the px widths are where the
 * columns start from, every column stands at its own where the column has the
 * room (1920), and where even its least width, every word whole, is wider
 * than the column, the table keeps that width and scrolls sideways in its own
 * frame, and only there (fixtures/tables.ts). Its frame keeps to the column,
 * and the page does not move.
 *
 * Read as a guest and as the primary seeded account, an administrator: that
 * viewer may pin a topic on every board, with a pin before every title, and
 * the pins of the pinned topics as the handles of their order. A moderator had
 * a column of pin buttons of his own past the dates, the widest form of the
 * table, with the column behind the sideways scroll at 1280 and 1366; the
 * pins stand in the title cell now, and the moderator has the columns of the
 * guest.
 */
/**
 * A window of the full version as a guest and as the administrator, each in
 * a context of its own and not the page of the project: a phone of a project
 * lays the page out at its own width whatever width it is given later
 * (fixtures/phone.ts).
 */
const test = base.extend<{ guest: Page; moderator: Page }>({
  guest: async ({ browser }, use) => {
    const context = await browser.newContext({
      viewport: { width: 1366, height: 900 },
    });
    await use(await context.newPage());
    await context.close();
  },
  moderator: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: PRIMARY_STORAGE_STATE,
      viewport: { width: 1366, height: 900 },
    });
    await use(await context.newPage());
    await context.close();
  },
});

const BOARD = "/forum/general";
const WIDTHS = [1024, 1280, 1366, 1440, 1920];

const COLUMNS = [
  "Топик",
  "Автор",
  "Комментарии",
  "Лайки",
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
 * wrong with it, the columns and the frame read by fixtures/tables.ts.
 */
async function measure(page: Page): Promise<Fit> {
  const tables = await page.evaluate(readTables, {
    within: ".topics-page",
    words: false,
  });
  const fit = await measureInPage(page);
  return { ...fit, problems: [...tables.problems, ...fit.problems] };
}

function measureInPage(page: Page): Promise<Fit> {
  return page.evaluate(() => {
    const problems: string[] = [];
    const scroller = document.querySelector<HTMLElement>(".main")!;
    const column = document.querySelector<HTMLElement>(".topics-page")!;
    const table = column.querySelector<HTMLElement>("table.data-table")!;
    const text = (el: HTMLElement) =>
      el.innerText.trim().replace(/\s+/g, " ").slice(0, 40);
    const label = (th: HTMLElement) =>
      (th.textContent ?? "").trim().replace(/\s+/g, " ");
    const shown = (el: Element) => el.getClientRects().length > 0;
    const textBoxes = (cell: Element) => {
      const range = document.createRange();
      range.selectNodeContents(cell);
      return [...range.getClientRects()].filter((r) => r.width > 0.5);
    };
    /** The words of a cell's text that stand on more than one line. */
    const brokenWords = (cell: Element) => {
      const broken: string[] = [];
      const walker = document.createTreeWalker(cell, NodeFilter.SHOW_TEXT);
      for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        // A word, with the break after a hyphen or a slash allowed.
        const words = /[^\s\-\u2010-\u2015/]+[-\u2010-\u2015/]?/g;
        for (const match of (node as Text).data.matchAll(words)) {
          if (match[0].length < 2) continue;
          const range = document.createRange();
          range.setStart(node, match.index!);
          range.setEnd(node, match.index! + match[0].length);
          const lines = new Set(
            [...range.getClientRects()]
              .filter((r) => r.width > 0.5)
              .map((r) => Math.round(r.top)),
          );
          if (lines.size > 1) broken.push(match[0]);
        }
      }
      return broken;
    };

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
    const heads = ths.filter(shown).map((th) => ({ th, boxes: textBoxes(th) }));
    for (const { th, boxes } of heads) {
      const cell = th.getBoundingClientRect();
      if (
        boxes.some(
          (r) => r.left < cell.left - 0.5 || r.right > cell.right + 0.5,
        )
      ) {
        problems.push(`the header "${text(th)}" runs out of its cell`);
      }
    }
    for (let i = 0; i < heads.length; i++) {
      for (let j = i + 1; j < heads.length; j++) {
        const meet = heads[i].boxes.some((a) =>
          heads[j].boxes.some(
            (b) =>
              a.left < b.right - 0.5 &&
              b.left < a.right - 0.5 &&
              a.top < b.bottom - 0.5 &&
              b.top < a.bottom - 0.5,
          ),
        );
        if (meet) {
          problems.push(
            `the headers "${text(heads[i].th)}" and "${text(heads[j].th)}" run into each other`,
          );
        }
      }
    }

    for (const cell of table.querySelectorAll<HTMLElement>("th, td")) {
      if (!shown(cell)) continue;
      if (cell.scrollWidth > cell.clientWidth + 1) {
        problems.push(
          `${cell.className} "${text(cell)}" overflows: ${cell.scrollWidth} > ${cell.clientWidth}`,
        );
      }
      if (getComputedStyle(cell).hyphens !== "none") {
        problems.push(`${cell.className} hyphenates`);
      }
      for (const word of brokenWords(cell)) {
        problems.push(`${cell.className} breaks "${word}"`);
      }
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

/** Two frames after the face has loaded: a table settles after its column. */
async function settle(page: Page) {
  await page.evaluate(() =>
    document.fonts.ready.then(
      () =>
        new Promise<void>((done) =>
          requestAnimationFrame(() => requestAnimationFrame(() => done())),
        ),
    ),
  );
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
      Лайки: 70,
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
