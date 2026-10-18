import { test, expect, SECONDARY_STORAGE_STATE } from "../../fixtures/auth";
import type { Browser, Locator, Page, Route } from "@playwright/test";

/**
 * The three blacklists of the site, as the owner asked for them on 13 October
 * 2026: "верни как было и добавить сделай нижней строкой", after the editor of
 * a character sheet, "где показано как делать добавление/удаление из таблицы".
 *
 * Each is the site's table "Пользователь" (the account's with "Внесен" too),
 * the bin at the end of each row, and the field and "Добавить" as the last
 * line of the table. The game and the blog say over it in a grey line what
 * the list does. No window anywhere, and every column stands.
 *
 * Read as TestUser, the master of the game "aaaam" in the seed. Nothing is
 * written to the stand: the lists are answered here, the press of "Добавить"
 * is answered here, and any other request that would write is refused on its
 * way out.
 */

// The stand is shared and answers 429 past 300 requests a minute; a read is
// asked again after a wait (fetchPastLimit), which a page of settings with its
// dozens of reads can take most of a minute through.
test.describe.configure({ timeout: 120_000 });

const LONG = "Повелитель_Драконов1";

const aUser = (username: string) => ({
  id: `u-${username}`,
  username,
  role: "RegularUser",
});

/** The game's list: three names, the longest login of the site among them. */
const MODULE_LIST = ["Player-Two", "RatingNegative", LONG].map(aUser);

type Entry = { id: string; username: string; createdUtc: string };

/** The account's list, newest first, as the server hands it. */
const PERSONAL: Entry[] = [
  { id: "e-1", username: "RatingNegative", createdUtc: "2026-09-21T10:00:00Z" },
  { id: "e-2", username: LONG, createdUtc: "2026-09-12T10:00:00Z" },
  { id: "e-3", username: "Player-Two", createdUtc: "2026-09-02T10:00:00Z" },
];

/** The name the field suggests and "Добавить" puts on. */
const NEWCOMER = "Mordred_Moon";

/** The full version, and the tablet, whose layout stays as it is. */
const WIDTHS = [768, 1366];

type Stand = {
  page: Page;
  /** The writes the page sent, method and path. */
  writes: string[];
  /** The searches the field sent, as their query strings. */
  searches: string[];
};

const json = (route: Route, body: unknown, status = 200) =>
  route
    .fulfill({
      status,
      contentType: "application/json",
      body: JSON.stringify(body),
    })
    .catch(() => {});

/**
 * A read sent on to the stand and asked again past its rate limit; null once
 * the page it was for is gone, which a test that is over leaves behind.
 */
async function fetchPastLimit(route: Route) {
  try {
    let response = await route.fetch();
    for (let i = 0; i < 10 && response.status() === 429; i += 1) {
      await new Promise((resolve) => setTimeout(resolve, 3000));
      response = await route.fetch();
    }
    return response;
  } catch {
    return null;
  }
}

async function passOn(route: Route) {
  const response = await fetchPastLimit(route);
  if (response) await route.fulfill({ response }).catch(() => {});
}

/** Shuts the page with its reads still on the wire left unanswered. */
async function close(page: Page) {
  await page.unrouteAll({ behavior: "ignoreErrors" });
  await page.context().close();
}

/**
 * Opens a path as TestUser at a width, the lists answered here: the game's
 * and the blog's with MODULE_LIST, the account's with PERSONAL, each with the
 * newcomer added once "Добавить" has put him on.
 */
async function open(
  browser: Browser,
  width: number,
  path: string,
): Promise<Stand> {
  const context = await browser.newContext({
    storageState: SECONDARY_STORAGE_STATE,
    viewport: { width, height: 900 },
  });
  const page = await context.newPage();
  const writes: string[] = [];
  const searches: string[] = [];
  let added = false;
  const newcomer = aUser(NEWCOMER);
  const newEntry: Entry = {
    id: "e-new",
    username: NEWCOMER,
    createdUtc: "2026-10-12T10:00:00Z",
  };

  await page.route(
    (url) => url.pathname.startsWith("/v1/"),
    async (route) => {
      const request = route.request();
      const method = request.method();
      const { pathname, search } = new URL(request.url());

      if (method !== "GET") {
        if (method === "OPTIONS" || method === "HEAD")
          return route.fallback().catch(() => {});
        writes.push(`${method} ${pathname}`);
        if (
          method === "POST" &&
          /^\/v1\/(games|blogs)\/[^/]+\/blacklist$/.test(pathname)
        ) {
          added = true;
          return json(route, { resource: newcomer }, 201);
        }
        if (method === "POST" && pathname === "/v1/users/me/blacklist") {
          added = true;
          return json(route, newEntry, 201);
        }
        return route.abort().catch(() => {});
      }

      if (/^\/v1\/(games|blogs)\/[^/]+\/blacklist$/.test(pathname)) {
        return json(route, {
          resources: added ? [...MODULE_LIST, newcomer] : MODULE_LIST,
        });
      }
      if (pathname === "/v1/users/me/blacklist") {
        const list = added ? [newEntry, ...PERSONAL] : PERSONAL;
        return json(route, {
          resources: list,
          paging: {
            pages: 1,
            current: 1,
            skip: 0,
            take: 50,
            total: list.length,
          },
        });
      }
      if (pathname === "/v1/users" && search.includes("search=")) {
        searches.push(search);
        return json(route, {
          resources: [aUser(NEWCOMER)],
          paging: { pages: 1, current: 1, skip: 0, take: 12, total: 1 },
        });
      }
      return passOn(route);
    },
  );

  await page.goto(path);
  // The lists above are answered here at once, the rest of the page by the
  // stand. Its sections that answer later (the sessions and the log of the
  // account) push the list down, and a field scrolled to before they come
  // stands off the screen with its suggestions by the time they are read.
  await page.waitForLoadState("networkidle");
  await page.evaluate(() => document.fonts.ready);
  return { page, writes, searches };
}

const settingsSection = (page: Page, title: string) =>
  page
    .locator(".settings-section")
    .filter({
      has: page.locator("h2", { hasText: new RegExp(`^${title}$`, "i") }),
    })
    .first();

const accountBlacklist = (page: Page) =>
  page.locator("section.section").filter({
    has: page.locator("h2.section-title", { hasText: /^Черный список$/ }),
  });

/**
 * The windows open on the page. The side columns of a narrow window are
 * dialogs too, always in the page and shut, and are not counted.
 */
const windows = (page: Page) =>
  page.locator('[role="dialog"]:not(.drawer-panel)');

/** The document is no wider than the window. */
async function noSidewaysPage(page: Page) {
  const { scroll, client } = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(scroll, "the page is wider than the window").toBeLessThanOrEqual(
    client,
  );
}

/**
 * The table of a list: its headers, the cells of every row, the name of every
 * bin, whether the field and "Добавить" are the line right under the box the
 * table scrolls in, and whether the table is wider than that box.
 */
async function readTable(root: Locator) {
  return root.evaluate((box) => {
    const table = box.querySelector("table")!;
    const scroll = box.querySelector(".data-table-scroll") as HTMLElement;
    const rows = [...table.querySelectorAll("tbody tr.table-row")];
    return {
      headers: [...table.querySelectorAll("thead th")].map((th) =>
        th.textContent!.trim(),
      ),
      cells: rows.map((tr) =>
        [...tr.querySelectorAll("td")].map((td) => td.textContent!.trim()),
      ),
      bins: rows.map(
        (tr) =>
          tr
            .querySelector("td:last-child button")
            ?.getAttribute("aria-label") ?? null,
      ),
      fieldUnderBox: !!box.querySelector(
        '.data-table-scroll + .bl-foot input[placeholder="Имя пользователя"]',
      ),
      buttonUnderBox: [
        ...box.querySelectorAll(".data-table-scroll + .bl-foot button"),
      ].some((b) => b.textContent!.trim() === "Добавить"),
      hidden: [...table.querySelectorAll("th, td")].filter(
        (cell) => getComputedStyle(cell).display === "none",
      ).length,
      scrollWidth: scroll.scrollWidth,
      clientWidth: scroll.clientWidth,
    };
  });
}

/**
 * Picks the newcomer from the suggestions of the field, with the field in the
 * middle of the window. The suggestion is whole on screen before it is
 * pressed: a box that scrolls sideways clips what opens down past its bottom
 * too, and a press would scroll that box to a suggestion nobody could see.
 */
async function pickNewcomer(root: Locator) {
  const field = root.getByPlaceholder("Имя пользователя");
  await field.evaluate((el) => el.scrollIntoView({ block: "center" }));
  await field.fill("Mord");
  const item = root.locator(".autocomplete-item", { hasText: NEWCOMER });
  await expect(item).toBeInViewport({ ratio: 1 });
  await item.click();
}

/** Every row of the table ends in the bin of its own name. */
function binsOf(names: string[]) {
  return names.map((name) => `Удалить ${name} из черного списка`);
}

for (const [where, path, hint] of [
  [
    "игры",
    "/game/aaaam/settings",
    "Внесенные не пишут в обсуждении игры и не подают заявки.",
  ],
  ["блога", "/blogs/aaaar/settings", "Внесенные не пишут комментарии в блоге."],
] as const) {
  test.describe(`Черный список ${where}`, () => {
    for (const width of WIDTHS) {
      test(`на ${width} это таблица "Пользователь", корзина в строке, поле последней строкой`, async ({
        browser,
      }) => {
        const { page } = await open(browser, width, path);
        const list = settingsSection(page, "Черный список");
        await expect(list.locator("tr.table-row").first()).toBeVisible({
          timeout: 60_000,
        });

        await expect(list.locator(".bl-hint")).toHaveText(hint);
        const table = await readTable(list);
        expect(table.headers).toEqual(["Пользователь", ""]);
        const names = ["Player-Two", "RatingNegative", LONG];
        expect(table.cells).toEqual(names.map((name) => [name, ""]));
        expect(table.bins).toEqual(binsOf(names));
        expect(table.fieldUnderBox).toBe(true);
        expect(table.buttonUnderBox).toBe(true);
        expect(table.hidden).toBe(0);
        expect(table.scrollWidth).toBeLessThanOrEqual(table.clientWidth);
        await expect(windows(page)).toHaveCount(0);
        await noSidewaysPage(page);
        await close(page);
      });
    }

    test("добавляет имя последней строкой, без окна, одним запросом", async ({
      browser,
    }) => {
      const { page, writes, searches } = await open(browser, 1366, path);
      const list = settingsSection(page, "Черный список");
      await expect(list.locator("tr.table-row").first()).toBeVisible({
        timeout: 60_000,
      });
      const add = list.getByRole("button", { name: "Добавить", exact: true });
      await expect(add).toBeDisabled();

      await pickNewcomer(list);
      await expect(add).toBeEnabled();
      await add.click();

      // The newcomer is the last row, right over the line that put him on,
      // and the field is empty again.
      await expect(list.locator("tr.table-row td:first-child")).toHaveText([
        "Player-Two",
        "RatingNegative",
        LONG,
        NEWCOMER,
      ]);
      await expect(list.getByPlaceholder("Имя пользователя")).toHaveValue("");
      await expect(windows(page)).toHaveCount(0);
      const sent = writes.filter((w) => w.includes("/blacklist"));
      expect(sent).toHaveLength(1);
      expect(sent[0]).toMatch(/^POST \/v1\/(games|blogs)\/[^/]+\/blacklist$/);
      // The field looks among everyone, the long absent too.
      expect(searches.length).toBeGreaterThan(0);
      expect(searches.every((s) => /[?&]activity=All(&|$)/.test(s))).toBe(true);
      await close(page);
    });
  });
}

test.describe("Черный список аккаунта", () => {
  for (const width of WIDTHS) {
    test(`на ${width} это та же таблица с днем внесения, поле последней строкой`, async ({
      browser,
    }) => {
      const { page } = await open(browser, width, "/account");
      const section = accountBlacklist(page);
      await expect(section.locator("tr.table-row").first()).toBeVisible({
        timeout: 60_000,
      });

      const table = await readTable(section);
      expect(table.headers).toEqual(["Пользователь", "Внесен", ""]);
      const names = ["RatingNegative", LONG, "Player-Two"];
      expect(table.cells.map((row) => row[0])).toEqual(names);
      for (const row of table.cells) {
        expect(row).toHaveLength(3);
        expect(row[1]).toMatch(/^\d\d\.\d\d\.\d{4}$/);
        expect(row[2]).toBe("");
      }
      expect(table.bins).toEqual(binsOf(names));
      expect(table.fieldUnderBox).toBe(true);
      expect(table.buttonUnderBox).toBe(true);
      expect(table.hidden).toBe(0);
      expect(table.scrollWidth).toBeLessThanOrEqual(table.clientWidth);
      // The settings stay over the list.
      await expect(section.locator(".setting-item")).toHaveCount(6);
      await noSidewaysPage(page);
      await close(page);
    });
  }

  test("добавляет имя первой строкой таблицы, без окна", async ({
    browser,
  }) => {
    const { page, writes, searches } = await open(browser, 1366, "/account");
    const section = accountBlacklist(page);
    await expect(section.locator("tr.table-row").first()).toBeVisible({
      timeout: 60_000,
    });
    const add = section.getByRole("button", { name: "Добавить", exact: true });
    await expect(add).toBeDisabled();

    await pickNewcomer(section);
    await add.click();

    // Newest first, as the server orders the list.
    await expect(
      section.locator("tr.table-row td:first-child").first(),
    ).toHaveText(NEWCOMER);
    await expect(section.locator("tr.table-row")).toHaveCount(4);
    await expect(section.getByPlaceholder("Имя пользователя")).toHaveValue("");
    await expect(windows(page)).toHaveCount(0);
    expect(writes.filter((w) => w.includes("/blacklist"))).toEqual([
      "POST /v1/users/me/blacklist",
    ]);
    expect(searches.every((s) => /[?&]activity=All(&|$)/.test(s))).toBe(true);
    await close(page);
  });
});
