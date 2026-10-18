import {
  test,
  expect,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";
import { PHONES, phoneContext } from "../../fixtures/phone";
import type { Browser, Page, Route } from "@playwright/test";

/**
 * Details of the full version on a phone (a phone shows the full version,
 * nothing cut, and the reader zooms in), each found on the live stand. A
 * phone lays the page out as a narrow window (fixtures/phone.ts), so each is
 * read on a phone as the phone lays it out:
 *
 * - the skeleton of a table stands the columns the rows stand, so no column
 *   goes or comes before the reader's eyes when the rows come;
 * - the lists of contest series and of achievement tiers are the site's table
 *   and do not scroll sideways, their main column and their actions in sight;
 * - a long post shows as many lines up to "показать полностью" on a phone as
 *   on 1366;
 * - the skeleton of a post stands its column beside the strips of the text, as
 *   the post does;
 * - the list of characters beside "Ждать хода" names the whole character;
 * - the text of a record of the log stands on a phone as on 1366;
 * - the middle column of a phone is no narrower than the middle column of
 *   1366, so what 1366 shows of a block a phone shows too.
 *
 * Read only. A list held back on its way to the page is held, not changed.
 */

type Reader = "guest" | "admin" | "user";

const SESSION: Record<Reader, string | undefined> = {
  guest: undefined,
  admin: PRIMARY_STORAGE_STATE,
  user: SECONDARY_STORAGE_STATE,
};

/** A phone of the given width, or a window of a desktop from 768 up. */
async function open(
  browser: Browser,
  reader: Reader,
  width: number,
  height = 844,
): Promise<Page> {
  const context =
    width < 768
      ? await phoneContext(browser, {
          storageState: SESSION[reader],
          width,
          height,
        })
      : await browser.newContext({
          storageState: SESSION[reader],
          viewport: { width, height },
        });
  return context.newPage();
}

/**
 * Holds every GET of the API path until `release` is called: the page draws
 * what it draws before the answer, a skeleton.
 */
async function hold(page: Page, path: RegExp): Promise<() => void> {
  let release!: () => void;
  const gate = new Promise<void>((done) => (release = done));
  await page.route(
    (url) => path.test(url.pathname),
    async (route: Route) => {
      if (route.request().method() !== "GET") return route.fallback();
      await gate;
      await route.continue().catch(() => {});
    },
  );
  return release;
}

/** The headers of the first table of the content column that stand. */
function standingHeaders(page: Page): Promise<string[]> {
  return page.evaluate(() =>
    [...document.querySelectorAll("main#main table.data-table thead th")]
      .filter((th) => getComputedStyle(th).display !== "none")
      .map((th) => (th.textContent ?? "").trim()),
  );
}

test.describe("the skeleton of a table on a phone", () => {
  for (const width of PHONES) {
    test(`takes no column away when the rows come, at ${width}px`, async ({
      browser,
    }) => {
      const page = await open(browser, "guest", width);
      const release = await hold(page, /^\/v1\/games$/);
      await page.goto("/games");
      await expect(
        page.locator("main#main tr.skeleton-row").first(),
      ).toBeVisible({ timeout: 20_000 });
      const skeleton = await standingHeaders(page);

      release();
      await expect(page.locator("main#main tr.table-row").first()).toBeVisible({
        timeout: 20_000,
      });
      const rows = await standingHeaders(page);

      // Every column stands in both, the main one among them.
      expect(skeleton).toContain("Название");
      expect(skeleton).toEqual(rows);
      await page.context().close();
    });
  }
});

/**
 * The two lists of the awards and the achievements of moderation used to be
 * tables of their own and scrolled sideways in their box on a phone, the
 * actions past the edge. An action of a tier is a sign named by its label
 * ("Редактировать тир ..."), an action of a series a word.
 */
test.describe("the lists of contest series and of achievement tiers on a phone", () => {
  const PAGES = [
    {
      url: "/moderation/awards",
      main: "Конкурс",
      action: /^(Скрыть|Вернуть)$/,
    },
    {
      url: "/moderation/achievements",
      main: "Название",
      action: /^(Редактировать|Удалить) тир /,
    },
  ];

  for (const width of PHONES) {
    for (const { url, main, action } of PAGES) {
      test(`${url} does not scroll and keeps its main column and its actions, at ${width}px`, async ({
        browser,
      }) => {
        const page = await open(browser, "admin", width);
        await page.goto(url);
        await expect(
          page.locator("main#main table tbody tr").first(),
        ).toBeVisible({ timeout: 20_000 });

        const tables = await page.evaluate(
          ([mainHeader, actionSource]) => {
            const actionWord = new RegExp(actionSource);
            return [...document.querySelectorAll("main#main table")].map(
              (table) => {
                const box = table.parentElement!;
                const frame = box.getBoundingClientRect();
                const shown = (el: Element) =>
                  getComputedStyle(el).display !== "none";
                const actions = [
                  ...table.querySelectorAll("tbody button"),
                ].filter((button) =>
                  actionWord.test(
                    (
                      button.getAttribute("aria-label") ??
                      button.textContent ??
                      ""
                    ).trim(),
                  ),
                );
                return {
                  scrolls: box.scrollWidth > box.clientWidth + 1,
                  main: [...table.querySelectorAll("thead th")]
                    .filter(shown)
                    .some((th) => (th.textContent ?? "").trim() === mainHeader),
                  actions: actions.length,
                  actionsInside: actions.every((button) => {
                    const r = button.getBoundingClientRect();
                    return (
                      r.width > 0 &&
                      r.left >= frame.left - 1 &&
                      r.right <= frame.right + 1
                    );
                  }),
                };
              },
            );
          },
          [main, action.source] as const,
        );

        expect(tables.length).toBeGreaterThan(0);
        for (const table of tables) {
          expect(table).toEqual({
            scrolls: false,
            main: true,
            actions: expect.any(Number),
            actionsInside: true,
          });
          expect(table.actions).toBeGreaterThan(0);
        }
        await page.context().close();
      });
    }
  }
});

/**
 * The height of the text of the best post of the week up to "показать
 * полностью". On 1366 it is as many lines as stand beside the left column; a
 * phone showed 150px of it, six lines where 1366 showed ten.
 */
async function collapsedText(browser: Browser, width: number) {
  const page = await open(browser, "guest", width);
  await page.goto("/");
  const card = page.locator("main#main .post-card").first();
  await expect(card.locator(".truncated-expand-button")).toBeVisible({
    timeout: 20_000,
  });
  // The budget comes once the left column is laid out, and the clip moves to
  // it smoothly: read it once it holds still.
  const clip = card.locator(".truncated-content").first();
  let height = -1;
  await expect
    .poll(
      async () => {
        const now = await clip.evaluate(
          (one) => one.getBoundingClientRect().height,
        );
        const still = now === height;
        height = now;
        return still;
      },
      { intervals: [300], timeout: 10_000 },
    )
    .toBe(true);
  const step = await card
    .locator(".game-text")
    .first()
    .evaluate((text) => parseFloat(getComputedStyle(text).lineHeight));
  await page.context().close();
  return { height, step };
}

test.describe("a long post on a phone", () => {
  test("shows as many lines up to the button as on 1366", async ({
    browser,
  }) => {
    const desktop = await collapsedText(browser, 1366);
    const phones = [
      await collapsedText(browser, 360),
      await collapsedText(browser, 390),
    ];

    // The same step of a line, and the same budget of lines: where it is cut
    // may differ by a line, as the paragraphs fall on other lines in a
    // narrower column.
    expect(phones.map((phone) => phone.step)).toEqual([
      desktop.step,
      desktop.step,
    ]);
    expect(
      phones.map(
        (phone) => Math.abs(phone.height - desktop.height) <= desktop.step,
      ),
      `360 and 390: ${phones.map((phone) => phone.height).join(" and ")} against ${desktop.height} on 1366`,
    ).toEqual([true, true]);
  });
});

test.describe("the skeleton of a post on a phone", () => {
  for (const width of [360, 390]) {
    test(`stands its column beside the strips of the text, as the post does, at ${width}px`, async ({
      browser,
    }) => {
      const page = await open(browser, "guest", width);
      const release = await hold(page, /^\/v1\/posts$/);
      await page.goto("/");
      const skeleton = page.locator("main#main .game-post-skeleton").first();
      await expect(skeleton).toBeVisible({ timeout: 20_000 });

      // The post stands its column beside its text on a phone as on 1366
      // (mobile-full-posts.spec.ts), so the strips start right of the column
      // and level with it, not under it.
      const [meta, body] = await Promise.all([
        skeleton.locator(".skeleton-meta").first().boundingBox(),
        skeleton.locator(".skeleton-body").first().boundingBox(),
      ]);
      expect(body!.x).toBeGreaterThanOrEqual(meta!.x + meta!.width - 0.5);
      expect(body!.y).toBeLessThan(meta!.y + meta!.height);

      release();
      await expect(page.locator("main#main .post-card").first()).toBeVisible();
      await page.context().close();
    });
  }
});

/**
 * The list a master picks a character from to wait for their turn was 120
 * wide at every width and cut "Арагорн Следопыт" to "Арагорн Сле". It is as
 * wide as its longest name and never wider than the column.
 */
test.describe('the list of characters beside "Ждать хода"', () => {
  for (const width of [360, 390, 1366]) {
    test(`names the whole character, at ${width}px`, async ({ browser }) => {
      // TestUser is the master of the game "aaaam" of the seed.
      const page = await open(browser, "user", width, 900);
      await page.goto("/game/aaaam/rooms/1");
      const select = page.locator("main#main .pending-select select");
      await expect(select).toBeVisible({ timeout: 20_000 });

      const fit = await select.evaluate((element) => {
        const list = element as HTMLSelectElement;
        const style = getComputedStyle(list);
        const pen = document.createElement("canvas").getContext("2d")!;
        pen.font = style.font;
        const widest = Math.max(
          ...[...list.options].map(
            (option) => pen.measureText(option.textContent!.trim()).width,
          ),
        );
        const room =
          list.clientWidth -
          parseFloat(style.paddingLeft) -
          parseFloat(style.paddingRight);
        const column = list.closest(".turns")!.getBoundingClientRect();
        const box = list.getBoundingClientRect();
        return {
          room,
          widest,
          inside: box.left >= column.left && box.right <= column.right + 0.5,
        };
      });

      expect(
        fit.room,
        `room ${fit.room} for a name ${fit.widest}`,
      ).toBeGreaterThanOrEqual(fit.widest);
      expect(fit.inside).toBe(true);
      await page.context().close();
    });
  }
});

/**
 * The text of a record stands on a phone as it stands on 1366, as the text of
 * a comment does: flush on both sides, its words broken at the end of a line
 * as the setting "Перенос слов" says, as DM2 sets it at every width.
 */
test.describe("the text of a record of the log on a phone", () => {
  test("stands as on 1366", async ({ browser }) => {
    const read = async (width: number) => {
      const page = await open(browser, "guest", width);
      await page.goto("/warnings");
      const text = page.locator("main#main .record-block__text").first();
      await expect(text).toBeVisible({ timeout: 20_000 });
      const style = await text.evaluate((el) => ({
        align: getComputedStyle(el).textAlign,
        hyphens: getComputedStyle(el).hyphens,
        lang: document.documentElement.lang,
      }));
      await page.context().close();
      return style;
    };

    const desktop = await read(1366);
    expect(desktop.align).toBe("justify");
    expect(desktop.lang).toBe("ru");
    expect(await read(390)).toEqual(desktop);
  });
});

/**
 * The middle column of a phone is as wide as the middle column of 1366 or
 * wider (index.html), so a block budgeted in lines of 1366, a folded news or
 * testimonial, shows on a phone no less than there.
 */
test.describe("the middle column of a phone", () => {
  test("is no narrower than on 1366", async ({ browser }) => {
    const width = async (screen: number) => {
      const page = await open(browser, "guest", screen, 900);
      await page.goto("/");
      const news = page.locator("main#main .news-list .topic-description");
      await expect(news.first()).toBeVisible({ timeout: 20_000 });
      const column = await page.locator("main#main").evaluate((main) => {
        const style = getComputedStyle(main);
        return (
          main.clientWidth -
          parseFloat(style.paddingLeft) -
          parseFloat(style.paddingRight)
        );
      });
      const text = (await news.first().boundingBox())!.width;
      await page.context().close();
      return { column, text };
    };

    const desktop = await width(1366);
    for (const phone of PHONES) {
      const found = await width(phone);
      expect(found.column, `${phone}`).toBeGreaterThanOrEqual(desktop.column);
      expect(found.text, `${phone}`).toBeGreaterThanOrEqual(desktop.text);
    }
    expect(desktop.column).toBeGreaterThan(600);
  });
});
