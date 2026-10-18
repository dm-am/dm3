import { test, expect } from "../../fixtures/auth";
import { PHONES, phoneContext } from "../../fixtures/phone";
import type { Browser, Page } from "@playwright/test";

/**
 * The rows of search and filters and the two tables of the rules page are the
 * full version's on a phone (the owner, 13 October 2026: no layout of the
 * phone's own, the reader zooms in). A phone lays the page out as a narrow
 * window (fixtures/phone.ts), and what stands there is what stands at 1366.
 *
 * - The search, "Фильтры" and the sort stand in one line, the buttons with
 *   their words and the side of every button of a page. Nothing runs past
 *   the bar.
 * - "Куда обращаться" stands in one line a row: the problem, the arrow and
 *   the solution, the solution at the size of the problem.
 * - The logins of the staff are whole, each on one line, no "..." cuts one,
 *   and the table does not scroll sideways.
 *
 * All read as a guest. The one answer changed is the list of administrators,
 * whose first login becomes twenty wide letters, the longest a login can be
 * (UsernamePolicy), on its way to the page; nothing is written.
 */

/** The full version. */
const DESKTOP = 1366;

/** The phones the owner's frames are taken on, and the full version. */
const WIDTHS = [...PHONES, DESKTOP];

/** The lists whose bar holds "Фильтры" and the sort. */
const LISTS = ["/games", "/community", "/blogs"];

/** A login of twenty wide letters. */
const LONG = "Ш".repeat(20);

/** The side of every button of a page, at every width. */
const PAGE_SIDE = "16px";

type Box = { left: number; right: number; top: number; bottom: number };

type Bar = {
  bar: Box;
  search: Box;
  buttons: {
    name: string;
    box: Box;
    /** The word on the button, as shown: empty when it is hidden. */
    word: string;
    wordBox: Box;
  }[];
  /** How far the bar's content runs past its own box. */
  overflow: number;
  /** How far the page runs past the window. */
  pageOverflow: number;
};

/** A phone of a width, or a window of 1366. */
function contextAt(browser: Browser, width: number) {
  return width < 1000
    ? phoneContext(browser, { width })
    : browser.newContext({ viewport: { width, height: 844 } });
}

async function open(browser: Browser, width: number, url: string) {
  const context = await contextAt(browser, width);
  const page = await context.newPage();
  await page.goto(url);
  return page;
}

/** The bar of the list, as laid out. */
async function readBar(page: Page): Promise<Bar> {
  await expect(page.locator(".filter-bar .sort-btn").first()).toBeVisible({
    timeout: 15000,
  });
  await page.evaluate(() => document.fonts.ready);
  return page.evaluate(() => {
    const boxOf = (el: Element): Box => {
      const { left, right, top, bottom } = el.getBoundingClientRect();
      return { left, right, top, bottom };
    };
    const bar = document.querySelector<HTMLElement>(".filter-bar")!;
    const buttons = [
      ...bar.querySelectorAll<HTMLElement>(".filter-btn, .sort-btn"),
    ].map((button) => {
      const word = button.querySelector<HTMLElement>(
        ".filter-btn-label, .sort-btn-label",
      )!;
      return {
        name: button.className,
        box: boxOf(button),
        word: word.offsetWidth > 0 ? word.innerText.trim() : "",
        wordBox: boxOf(word),
      };
    });
    return {
      bar: boxOf(bar),
      search: boxOf(bar.querySelector(".search-container")!),
      buttons,
      overflow: bar.scrollWidth - bar.clientWidth,
      pageOverflow:
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    };
  });
}

/** The search, then the buttons, in one line, the words shown, inside the bar. */
function expectOneLine({ bar, search, buttons, overflow, pageOverflow }: Bar) {
  const middle = (box: Box) => (box.top + box.bottom) / 2;
  expect(buttons).toHaveLength(2);
  expect(buttons[0]!.word).toBe("Фильтры");
  expect(buttons[1]!.word).not.toBe("");
  let left = search.right;
  for (const { name, box, wordBox } of buttons) {
    expect(Math.abs(middle(box) - middle(search)), name).toBeLessThan(1);
    expect(box.left, name).toBeGreaterThan(left);
    expect(wordBox.right, name).toBeLessThanOrEqual(box.right + 0.5);
    expect(box.right, name).toBeLessThanOrEqual(bar.right + 0.5);
    left = box.right;
  }
  expect(overflow).toBeLessThanOrEqual(0);
  expect(pageOverflow).toBeLessThanOrEqual(0);
}

/** "Фильтры" and the sort keep the side of every button of a page. */
async function expectPageSides(page: Page) {
  const pair = page
    .locator(".filter-bar")
    .first()
    .locator(".filter-btn, .sort-btn");
  await expect(pair).toHaveCount(2);
  for (const button of await pair.all()) {
    await expect(button).toHaveCSS("padding-left", PAGE_SIDE);
    await expect(button).toHaveCSS("padding-right", PAGE_SIDE);
  }
}

test.describe("The bar of a list", () => {
  for (const url of LISTS) {
    for (const width of WIDTHS) {
      test(`${url} at ${width}: one line, as at 1366`, async ({ browser }) => {
        const page = await open(browser, width, url);
        expectOneLine(await readBar(page));
        await expectPageSides(page);

        await page.context().close();
      });
    }
  }
});

/**
 * The lists whose sort has a long name ("Последняя активность", "Последние
 * оцененные", the longest of the site "Дата последнего набора"): on a phone
 * they stand in the line of the search as well, as at 1366.
 */
const LONG_SORTS = [
  "/forum/general",
  "/forum/general/4",
  "/blogs",
  "/users/TestUser?tab=topics",
  "/users/TestUser/received-reviews",
  "/users/TestUser/given-reviews",
  "/game/aaaaa/post-reviews",
  "/games?sortBy=availableslots",
];

test.describe("A long sort on a phone of 390", () => {
  for (const url of LONG_SORTS) {
    test(`${url}: the search, "Фильтры" and the sort in one line`, async ({
      browser,
    }) => {
      const page = await open(browser, 390, url);
      expectOneLine(await readBar(page));
      await expectPageSides(page);

      await page.context().close();
    });
  }
});

type HelpRow = {
  problem: Box;
  arrow: Box & { shown: boolean };
  solution: Box;
  /** The top of the first line of the arrow and of the solution. */
  arrowLine: number;
  solutionLine: number;
  problemSize: string;
  solutionSize: string;
};

function readHelp(page: Page): Promise<HelpRow[]> {
  return page.evaluate(() => {
    const boxOf = (el: Element) => {
      const { left, right, top, bottom } = el.getBoundingClientRect();
      return { left, right, top, bottom };
    };
    const firstLine = (el: Element) => {
      const range = document.createRange();
      range.selectNodeContents(el);
      const rects = [...range.getClientRects()].filter((r) => r.width > 0);
      return rects.length ? Math.min(...rects.map((r) => r.top)) : NaN;
    };
    return [...document.querySelectorAll(".help-row")].map((row) => {
      const problem = row.querySelector(".help-problem-col")!;
      const arrow = row.querySelector<HTMLElement>(".help-arrow-col")!;
      const solution = row.querySelector(".help-solution-col")!;
      return {
        problem: boxOf(problem),
        arrow: { ...boxOf(arrow), shown: arrow.offsetWidth > 0 },
        solution: boxOf(solution),
        arrowLine: firstLine(arrow),
        solutionLine: firstLine(solution),
        problemSize: getComputedStyle(problem).fontSize,
        solutionSize: getComputedStyle(solution).fontSize,
      };
    });
  });
}

test.describe("Куда обращаться", () => {
  for (const width of WIDTHS) {
    test(`at ${width}: one line a row, as at 1366`, async ({ browser }) => {
      const page = await open(browser, width, "/rules");
      await expect(page.locator(".help-row").first()).toBeVisible({
        timeout: 15000,
      });
      await page.evaluate(() => document.fonts.ready);
      const rows = await readHelp(page);

      expect(rows.length).toBeGreaterThan(0);
      for (const row of rows) {
        expect(row.arrow.shown).toBe(true);
        expect(row.arrow.left).toBeGreaterThan(row.problem.right);
        expect(row.solution.left).toBeGreaterThanOrEqual(row.arrow.right);
        expect(Math.abs(row.arrowLine - row.solutionLine)).toBeLessThan(2);
        expect(row.solutionSize).toBe(row.problemSize);
      }

      await page.context().close();
    });
  }
});

type Staff = {
  /** How far the table's rows run past its box. */
  overflow: number;
  logins: {
    text: string;
    /** Elements of the cell that cut their text with "…". */
    cut: number;
    /** How far its farthest letter stands past the edge of the table. */
    overrun: number;
    lines: number;
  }[];
};

/** The staff table of the rules, the first administrator's login the long one. */
async function openStaff(browser: Browser, width: number): Promise<Staff> {
  const context = await contextAt(browser, width);
  const page = await context.newPage();
  await page.route(
    (url) =>
      /\/v1\/users$/.test(url.pathname) &&
      url.searchParams.get("role") === "Admin",
    async (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const response = await route.fetch();
      const answer = (await response.json()) as {
        resources?: { username: string }[];
      };
      if (answer.resources?.[0]) answer.resources[0].username = LONG;
      await route.fulfill({ response, json: answer });
    },
  );
  await page.goto("/rules");
  await expect(page.locator("#admins .user-item").first()).toBeVisible({
    timeout: 15000,
  });
  await page.evaluate(() => document.fonts.ready);
  const staff = await page.evaluate(() => {
    const list = document.querySelector<HTMLElement>(
      "#admins .expandable-list",
    )!;
    const edge = list.getBoundingClientRect().right;
    const logins = [
      ...list.querySelectorAll<HTMLElement>(".user-item .user-link"),
    ].map((link) => {
      const range = document.createRange();
      range.selectNodeContents(link);
      const rects = [...range.getClientRects()].filter((r) => r.width > 0);
      const cell = link.closest(".row-cell") ?? link;
      return {
        text: link.textContent!.trim(),
        cut: [cell, ...cell.querySelectorAll("*")].filter(
          (el) => getComputedStyle(el).textOverflow === "ellipsis",
        ).length,
        overrun: Math.max(...rects.map((r) => r.right)) - edge,
        lines: new Set(rects.map((r) => Math.round(r.top))).size,
      };
    });
    return { overflow: list.scrollWidth - list.clientWidth, logins };
  });
  await context.close();
  return staff;
}

test.describe("The staff of the rules", () => {
  for (const width of WIDTHS) {
    test(`at ${width}: every login whole on one line, the table does not scroll`, async ({
      browser,
    }) => {
      const { overflow, logins } = await openStaff(browser, width);

      expect(logins.map((login) => login.text)).toContain(LONG);
      expect(logins.filter((login) => login.lines !== 1)).toEqual([]);
      expect(logins.filter((login) => login.cut > 0)).toEqual([]);
      expect(logins.filter((login) => login.overrun > 0.5)).toEqual([]);
      expect(overflow).toBeLessThanOrEqual(0);
    });
  }
});
