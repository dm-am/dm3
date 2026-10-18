import {
  test,
  expect,
  API_BASE_URL,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";
import { phonePage } from "../../fixtures/phone";
import { readTables } from "../../fixtures/tables";
import type { Browser, Page } from "@playwright/test";

/**
 * Every table of the content column keeps all of its columns and every word
 * whole at every width, a phone included, and never widens the page.
 *
 * The column is narrow: 300px at 1024, 556px at 1280, 642px at 1366 and on a
 * phone, 716px at 1440 and 1196px at 1920; the sidebars take the rest. A table
 * shows all of its columns at the widths they declare, its text breaks between
 * words only, as on DM2, and where even its least width, every word whole, is
 * wider than the column, the table scrolls sideways in its own frame
 * (DataTable) while the page stays put, and only there. That choice stands
 * while the owner's answer on tables wider than the column is open
 * (fixtures/tables.ts).
 *
 * What is checked is what a reader sees, at every width in turn: every column
 * is there, in the header and in every row; no two headers run into each
 * other, no cell holds more than its width, no word stands on two lines and
 * none is hyphenated; the frame keeps to the column and scrolls only a table
 * wider than the column at its least; nothing between the frame and the
 * column scrolls, and the page does not.
 */
const WIDTHS = [1024, 1280, 1366, 1440, 1920];

/** The widths the statistics are read at while their boards load. */
const STATISTICS_WIDTHS = [1280, 1366, 1440, 1920];

/** The seeded account the profile tables are read on: it hosts, plays, blogs. */
const PROFILE = "/users/SolohinLex";

/** A seeded game with both rosters, the players' and the master's. */
const GAME_TITLE = "Хроники Забытых Королевств";

/** A seeded blog; every blog of the seed has the rubrics "Общее" and "Для избранных". */
const BLOG_TITLE = "Заметки мастера";

/** Rows of a table that has answered, or its empty state. */
const ROWS = "main#main tr.table-row, main#main tr.table-empty-row";

/** The descriptor column is named by the game's schema, not by the page. */
const DESCRIPTOR = expect.any(String);

interface Target {
  name: string;
  open: (page: Page) => Promise<void>;
  /** The headers of each table of the page, in order. */
  headers?: unknown[][];
  /** The headers of every table of a page with as many as the seed has. */
  each?: string[];
}

async function openAndWait(page: Page, url: string, ready = ROWS) {
  await page.goto(url);
  await expect(page.locator(ready).first()).toBeVisible({ timeout: 15000 });
  // A skeleton row is not what is measured.
  await expect(page.locator("main#main tr.skeleton-row")).toHaveCount(0, {
    timeout: 15000,
  });
}

/** Opens a path and waits for the rows of its tables. */
function path(url: string, headers: unknown[][], ready = ROWS): Target {
  return { name: url, headers, open: (page) => openAndWait(page, url, ready) };
}

/** Three names of the account's blacklist, the longest login of the site among them. */
const BLACKLISTED = [
  { id: "e-1", username: "RatingNegative", createdUtc: "2026-09-21T10:00:00Z" },
  {
    id: "e-2",
    username: "Повелитель_Драконов1",
    createdUtc: "2026-09-12T10:00:00Z",
  },
  { id: "e-3", username: "Player-Two", createdUtc: "2026-09-02T10:00:00Z" },
];

/**
 * The account's own blacklist answered here: the seed lists nobody in it.
 * Nothing is written, the list is only read.
 */
async function answerBlacklist(page: Page, list: typeof BLACKLISTED) {
  await page.route(
    (url) => url.pathname === "/v1/users/me/blacklist",
    (route) =>
      route.fulfill({
        json: {
          resources: list,
          paging: {
            pages: 1,
            current: 1,
            skip: 0,
            take: 50,
            total: list.length,
          },
        },
      }),
  );
}

const GAMES = [
  "#",
  "Название",
  "Ведущие",
  "Теги",
  "Статус игры",
  "Рецензии",
  "Читатели",
];
const BLOGS = ["#", "Название", "Ведущие", "Статус блога", "Читатели"];

const GUEST: Target[] = [
  path("/games", [GAMES]),
  path("/blogs", [BLOGS]),
  path("/forum", [
    ["Раздел", "Описание", "Топики", "Комментарии", "Последняя активность"],
  ]),
  path("/community", [
    [
      "#",
      "Имя пользователя",
      "Активность",
      "Рейтинг",
      "Рекомендации",
      "Рецензии",
      "Игры",
      "Блоги",
      "Регистрация",
    ],
  ]),
  path(`${PROFILE}?tab=games`, [
    ["#", "Название", "Ведущие", "Статус игры", "Рецензии", "Читатели"],
  ]),
  path(`${PROFILE}?tab=blogs`, [BLOGS]),
  path(`${PROFILE}?tab=topics`, [
    [
      "Топик",
      "Раздел",
      "Комментарии",
      "Лайки",
      "Дата создания",
      "Последняя активность",
    ],
  ]),
  {
    name: "game page",
    headers: [
      [
        "#",
        "Игрок",
        "Рейтинг",
        "Присутствие",
        "Имя персонажа",
        DESCRIPTOR,
        "Посты",
        "Последний пост",
        "Статус",
      ],
      ["#", "Имя персонажа", DESCRIPTOR, "Посты", "Последний пост", "Статус"],
    ],
    open: async (page) => {
      await openAndWait(page, "/games");
      const href = await page
        .locator("main#main a.game-link", { hasText: GAME_TITLE })
        .first()
        .getAttribute("href");
      expect(href).toBeTruthy();
      // The rosters answer after the facts of the game.
      await openAndWait(page, href!, "main#main .roster tr.table-row");
      await expect(page.locator("main#main .roster")).toHaveCount(2);
    },
  },
];

const ADMIN: Target[] = [
  {
    name: `${PROFILE}?tab=games as a player`,
    headers: [
      [
        "#",
        "Название",
        "Ведущие",
        "Статус игры",
        "Статус персонажа",
        "Рецензии",
        "Читатели",
      ],
    ],
    open: async (page) => {
      await openAndWait(page, `${PROFILE}?tab=games`);
      await page
        .getByRole("group", { name: "Роль в играх" })
        .getByRole("button", { name: "Игрок" })
        .click();
      await expect(
        page.locator("main#main thead th", { hasText: "Статус персонажа" }),
      ).toHaveCount(1);
      await expect(page.locator("main#main tr.skeleton-row")).toHaveCount(0);
    },
  },
  path(`${PROFILE}/uploads`, [["Превью", "Файл", "Дата", "Размер", ""]]),
  path("/moderation/violators", [
    ["Пользователь", "Баллы", "Последнее предупреждение", "Активный бан"],
  ]),
  path("/moderation/uploads", [
    ["Превью", "Файл", "Загрузил", "Дата", "Размер", ""],
  ]),
  path("/moderation/removed-accounts", [
    [
      "Пользователь",
      "Почта",
      "Роль",
      "Регистрация",
      "Последняя активность",
      "",
    ],
  ]),
  // The series of the contests and the tiers of the achievements, a table of
  // tiers for every category of the seed.
  path("/moderation/awards", [["Конкурс", "Год", "Топик", "Статус", ""]]),
  {
    name: "/moderation/achievements",
    each: ["Тир", "Код", "Название", "Порог", ""],
    open: (page) => openAndWait(page, "/moderation/achievements"),
  },
  // The account's own blacklist, empty as the seed has it: the table is read
  // by its header and its empty row.
  path("/account", [["Пользователь", "Внесен", ""]]),
  // And with three names in it, the longest login of the site among them.
  {
    name: "/account with names",
    headers: [["Пользователь", "Внесен", ""]],
    open: async (page) => {
      await answerBlacklist(page, BLACKLISTED);
      await openAndWait(page, "/account", "main#main .bl-table tr.table-row");
    },
  },
];

/** Pages read as TestUser, whose own notifications the seed writes. */
const USER: Target[] = [
  path("/notifications", [["Событие", "Где", "Кто", "Когда"]]),
];

interface Fit {
  /** The headers of each table, in order. */
  headers: string[][];
  problems: string[];
}

/**
 * The headers of every DataTable of the content column, and everything wrong
 * with those tables that fixtures/tables.ts does not read, as sentences. The
 * column's edges are the shell's, not the page's own box: a table that is too
 * wide widens that box together with itself.
 */
function edgesAndHeads(): string[] {
  const problems: string[] = [];
  const scroller = document.querySelector<HTMLElement>(".main")!;
  const content = document.querySelector<HTMLElement>("main#main")!;
  const style = getComputedStyle(content);
  // The shell around the column: the sidebars on a desktop, nothing beside it
  // on a phone and up to 1000, where they slide in over the page.
  const body = document
    .querySelector<HTMLElement>(".content-body")!
    .getBoundingClientRect();
  const asideLeft = document.querySelector<HTMLElement>("aside.sidebar-left");
  const asideRight = document.querySelector<HTMLElement>("aside.sidebar-right");
  const left =
    (asideLeft ? asideLeft.getBoundingClientRect().right : body.left) +
    parseFloat(style.paddingLeft);
  const right =
    (asideRight ? body.right - asideRight.offsetWidth : body.right) -
    parseFloat(style.paddingRight);

  if (scroller.scrollWidth > scroller.clientWidth + 1) {
    problems.push(
      `the page scrolls sideways: ${scroller.scrollWidth} > ${scroller.clientWidth}`,
    );
  }

  const shown = (el: Element) =>
    el.getClientRects().length > 0 &&
    getComputedStyle(el).visibility !== "hidden";
  const text = (el: HTMLElement) =>
    el.innerText.trim().replace(/\s+/g, " ").slice(0, 40);
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}.${[...el.classList].join(".")}`;
  /** The children of a box with their drawn and scrolling widths, for a run without a browser. */
  const widths = (box: HTMLElement) =>
    [...box.children]
      .filter((c): c is HTMLElement => c instanceof HTMLElement && shown(c))
      .map((c) => `${name(c)} ${c.offsetWidth}/${c.scrollWidth} "${text(c)}"`)
      .join(", ");
  /** The boxes of the text a cell draws, one per line. */
  const textBoxes = (cell: Element) => {
    const range = document.createRange();
    range.selectNodeContents(cell);
    return [...range.getClientRects()].filter((r) => r.width > 0.5);
  };

  const tables = [
    ...content.querySelectorAll<HTMLElement>("table.data-table"),
  ].filter(shown);
  for (const table of tables) {
    // The frame of the table keeps to the column, and nothing between the
    // two scrolls: the frame is the one box that may.
    const frame = table.parentElement!;
    const edge = frame.getBoundingClientRect();
    if (edge.left < left - 0.5 || edge.right > right + 0.5) {
      problems.push(
        `the frame of ${name(table)} spans ${Math.round(edge.left)}-${Math.round(edge.right)}, the column ${Math.round(left)}-${Math.round(right)}`,
      );
    }
    for (let up = frame.parentElement; up && up !== content; ) {
      if (up.scrollWidth > up.clientWidth + 1) {
        problems.push(
          `${name(up)} scrolls sideways: ${up.scrollWidth} > ${up.clientWidth}, children ${widths(up)}`,
        );
      }
      up = up.parentElement;
    }

    // No header runs into another: the text of each stays inside its own
    // cell, so no two of them meet.
    const heads = [...table.querySelectorAll<HTMLElement>("thead th")]
      .filter(shown)
      .map((th) => ({ th, boxes: textBoxes(th) }));
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

    // Content running past the cell's own edge, clipped or not.
    for (const cell of table.querySelectorAll<HTMLElement>("th, td")) {
      if (shown(cell) && cell.scrollWidth > cell.clientWidth + 1) {
        problems.push(
          `${name(cell)} "${text(cell)}" overflows: ${cell.scrollWidth} > ${cell.clientWidth}`,
        );
      }
    }
  }
  return problems;
}

/** Everything wrong with the tables of the content column as they stand. */
async function measure(page: Page): Promise<Fit> {
  const tables = await page.evaluate(readTables, {
    within: "main#main",
    words: true,
  });
  return {
    headers: tables.headers,
    problems: [...tables.problems, ...(await page.evaluate(edgesAndHeads))],
  };
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

/** Every header of every table of the page, as the target declares them. */
function expectedHeaders(target: Target, all: string[][]): unknown[][] {
  return target.each ? all.map(() => target.each!) : target.headers!;
}

/** Reads the page at every width in turn, once its tables have settled. */
async function expectFits(page: Page, target: Target) {
  await page.setViewportSize({ width: WIDTHS[0], height: 900 });
  await settle(page);
  const first = await measure(page);
  expect(first.headers.length).toBeGreaterThan(0);
  expect(first.headers).toEqual(expectedHeaders(target, first.headers));
  const problems: string[] = [];
  for (const width of WIDTHS) {
    await page.setViewportSize({ width, height: 900 });
    await settle(page);
    const fit = await measure(page);
    problems.push(...fit.problems.map((one) => `${width}px: ${one}`));
  }
  expect(problems).toEqual([]);
}

/**
 * A page of a window of the full version, as the session given or as a guest;
 * it closes with its context. A window of its own and not the page of the
 * project: a phone of a project lays the page out at its own width whatever
 * the width it is given later (fixtures/phone.ts).
 */
async function windowPage(
  browser: Browser,
  width: number,
  storageState?: string,
): Promise<Page> {
  const context = await browser.newContext({
    storageState,
    viewport: { width, height: 900 },
  });
  return context.newPage();
}

/** The readers of the tables, by the session each reads in. */
const READERS = [
  { who: "a guest", targets: GUEST, session: undefined },
  { who: "an administrator", targets: ADMIN, session: PRIMARY_STORAGE_STATE },
  { who: "a user", targets: USER, session: SECONDARY_STORAGE_STATE },
];

for (const { who, targets, session } of READERS) {
  test.describe(`Tables of the content column, as ${who}`, () => {
    test.describe.configure({ timeout: 90_000 });
    for (const target of targets) {
      test(`${target.name} keeps its columns and words at ${WIDTHS.join(", ")}px`, async ({
        browser,
      }) => {
        const page = await windowPage(browser, WIDTHS[0], session);
        await target.open(page);
        await expectFits(page, target);
        await page.context().close();
      });
    }
  });
}

test.describe("Statistics while its boards load", () => {
  for (const width of STATISTICS_WIDTHS) {
    test(`does not scroll sideways at ${width}px`, async ({ browser }) => {
      const page = await windowPage(browser, width);
      // The boards are held back until the loading cards are measured.
      let release = () => {};
      const held = new Promise<void>((resolve) => (release = resolve));
      await page.route("**/v1/leaderboards/**", async (route) => {
        await held;
        await route.continue();
      });
      await page.goto("/statistics");
      await expect(
        page.locator("main#main .stat-board-skeleton-row").first(),
      ).toBeVisible({ timeout: 15000 });

      const overflow = await page.evaluate(() => {
        const scroller = document.querySelector<HTMLElement>(".main")!;
        return scroller.scrollWidth - scroller.clientWidth;
      });
      release();
      await page.context().close();
      expect(overflow).toBeLessThanOrEqual(0);
    });
  }
});

/**
 * The tables of a page on a phone. A phone lays the page out as a narrow
 * window whose column is as wide as at 1366 (fixtures/phone.ts), and its
 * tables are the tables of the full version, by the same rule.
 */
async function expectFitsOnPhone(page: Page, target: Target) {
  await settle(page);
  const fit = await measure(page);
  expect(fit.headers).toEqual(expectedHeaders(target, fit.headers));
  expect(fit.problems.map((one) => `a phone: ${one}`)).toEqual([]);
}

for (const { who, targets, session } of READERS) {
  test.describe(`Tables of the content column on a phone, as ${who}`, () => {
    for (const target of targets) {
      test(`${target.name} keeps its columns and words on a phone`, async ({
        browser,
      }) => {
        const page = await phonePage(browser, { storageState: session });
        await target.open(page);
        await expectFitsOnPhone(page, target);
        await page.context().close();
      });
    }
  });
}

/** The number of lines each element of `selector` stands on. */
function linesOf(page: Page, selector: string): Promise<number[]> {
  return page.locator(selector).evaluateAll((cells) =>
    cells.map((cell) => {
      const range = document.createRange();
      range.selectNodeContents(cell);
      return new Set(
        [...range.getClientRects()]
          .filter((r) => r.width > 0.5)
          .map((r) => Math.round(r.top)),
      ).size;
    }),
  );
}

/**
 * The share a header declares leaves the main column its line where the
 * column has the room: the rubrics of a blog stand on one line at 1366, where
 * "Публикаций" keeps its half of the table to the pixel, and on a phone, whose
 * column is as wide. "Для избранных" once stood on two lines on a phone, its
 * column 101 wide beside a column of one digit 161 wide.
 */
test.describe("A share leaves the main column its line", () => {
  test("the rubrics of a blog stand on one line on a phone, and keep their shares at 1366", async ({
    request,
    browser,
  }) => {
    const blogs = await request.get(`${API_BASE_URL}/v1/blogs?take=100`);
    expect(blogs.ok(), `GET /v1/blogs answered ${blogs.status()}`).toBe(true);
    const blog = ((await blogs.json()).resources ?? []).find(
      (one: { title: string }) => one.title === BLOG_TITLE,
    );
    expect(blog, `the seed has no blog "${BLOG_TITLE}"`).toBeTruthy();
    const address = `/blogs/${blog.publicId ?? blog.id}`;
    const cells = "main#main .rubrics td.col-title, main#main .rubrics th";
    const problems: string[] = [];

    const phone = await phonePage(browser);
    await openAndWait(phone, address, "main#main .rubrics tr.table-row");
    await settle(phone);
    const onPhone = await linesOf(phone, cells);
    if (onPhone.some((count) => count !== 1))
      problems.push(`a phone: lines ${onPhone.join(", ")}`);
    await phone.context().close();

    const page = await windowPage(browser, 1366);
    await openAndWait(page, address, "main#main .rubrics tr.table-row");
    await settle(page);
    const onDesktop = await linesOf(page, cells);
    if (onDesktop.some((count) => count !== 1))
      problems.push(`1366px: lines ${onDesktop.join(", ")}`);
    const share = await page
      .locator("main#main .rubrics th", { hasText: "Публикаций" })
      .getAttribute("style");
    if (share !== "width: 50%;")
      problems.push(`1366px: "Публикаций" stands at ${share}`);
    await page.context().close();
    expect(problems).toEqual([]);
  });

  // The account's blacklist with nobody in it: the shares of "Внесен" and of
  // the action once left "Пользователь" less than its line, and the header
  // stood on two lines over the empty row.
  test("the header of an empty blacklist stands on one line on a phone", async ({
    browser,
  }) => {
    const page = await phonePage(browser, {
      storageState: PRIMARY_STORAGE_STATE,
    });
    await answerBlacklist(page, []);
    await openAndWait(
      page,
      "/account",
      "main#main .bl-table tr.table-empty-row",
    );
    await settle(page);

    const shown = await page
      .locator("main#main .bl-table thead th:visible")
      .allTextContents();
    const lines = await linesOf(page, "main#main .bl-table thead th");
    expect(shown).toHaveLength(3);
    expect(lines.filter((count) => count > 1)).toEqual([]);
    await page.context().close();
  });
});
