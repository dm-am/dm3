import { test, expect, type Locator, type Page } from "@playwright/test";
import { LAYOUT_WIDTH, phonePage } from "../../fixtures/phone";

/**
 * What the sliding column of a narrow screen holds (app/App.vue,
 * widgets/sidebar): the two columns of a desktop as two views of one column,
 * switched by the tabs of the site in its bar (owner, 13 October 2026: one
 * button, the left column, and a way to the right one and back).
 *
 * The left view, "Меню", opens on the menu of the header as the header of a
 * desktop draws it: the same links in the same type, ink and tracking, from
 * the left, wrapping between links. The right view, "О сайте", opens on the
 * statistics of the header as the header draws them, holds the whole right
 * column, its four last blocks included (the address of the support, the
 * fundraising, the partners, the socials), and closes on the documents of the
 * site. Nothing of the right column stands under the page, and the settings
 * are the gear's, not the column's.
 *
 * A page a letter leads to holds its card alone at every width: its column
 * holds the menu and the statistics, which the header of a desktop has, and
 * none of the columns' blocks.
 *
 * The frame of the column (its bar, the cross, the scrim) is the frame's own
 * to check, in phone-drawer.spec.ts.
 */

const DESKTOP = { width: 1366, height: 768 };

/** The menu of the site a guest sees, in order (shared/config/mainNavigation). */
const GUEST_SECTIONS = [
  "О проекте",
  "Правила",
  "Игры",
  "Блоги",
  "Сообщество",
  "Форум",
  "Чат",
  "Для новичков",
];

/** The blocks that end the right column of a desktop. */
const TAIL = [
  "Обратная связь",
  "Поддержка проекта",
  "Партнеры",
  "Наши соцсети",
];

/** Opens the column by the button of the header, on its left view. */
async function openColumn(page: Page): Promise<Locator> {
  await page.getByTestId("menu-button").click();
  const column = page.getByRole("dialog", { name: "Меню" });
  await expect(column).toBeVisible();
  await expect(column.getByRole("tab", { name: "Меню" })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  return column;
}

/** The look of the links of a menu and of the lines of the statistics. */
function lookOf(page: Page, menu: string, stats: string) {
  return page.evaluate(
    ([menuSelector, statsSelector]) => {
      const link = document.querySelector(`${menuSelector} a`)!;
      const linkStyle = getComputedStyle(link);
      const statsStyle = getComputedStyle(
        document.querySelector(statsSelector)!,
      );
      return {
        link: [
          linkStyle.fontFamily,
          linkStyle.fontSize,
          linkStyle.letterSpacing,
          linkStyle.color,
          linkStyle.fontWeight,
        ],
        stats: [
          statsStyle.fontFamily,
          statsStyle.fontSize,
          statsStyle.lineHeight,
          statsStyle.color,
        ],
        rows: document.querySelectorAll(`${statsSelector} .stat-row`).length,
      };
    },
    [menu, stats],
  );
}

/**
 * Every heading of a sidebar block named so, wherever it stands: how many
 * there are, and for each whether it is inside the sliding column.
 */
function headingsNamed(page: Page, titles: string[]) {
  return page.evaluate(
    (names) =>
      names.map((name) => {
        const found = [
          ...document.querySelectorAll<HTMLElement>(".sidebar-title"),
        ].filter(
          (heading) =>
            (heading.textContent ?? "").trim().toLowerCase() ===
            name.toLowerCase(),
        );
        return {
          name,
          count: found.length,
          inColumn: found.some(
            (heading) => heading.closest('[role="dialog"]') !== null,
          ),
        };
      }),
    titles,
  );
}

test.describe("the column of a phone", () => {
  test("leads its left view with the menu of the header, drawn as on 1366", async ({
    page,
    browser,
  }) => {
    // The header of 1366 is the model.
    await page.setViewportSize(DESKTOP);
    await page.goto("/");
    await expect(page.locator("header .site-stats")).toBeVisible({
      timeout: 15000,
    });
    const desktop = await lookOf(
      page,
      "header nav.top-menu",
      "header .site-stats",
    );

    const phone = await phonePage(browser);
    await phone.emulateMedia({ reducedMotion: "reduce" });
    await phone.goto("/");
    const column = await openColumn(phone);
    const menu = column.getByRole("navigation", { name: "Основные разделы" });
    await expect(menu.getByRole("link")).toHaveText(GUEST_SECTIONS);

    const look = await lookOf(
      phone,
      ".drawer-panel nav.main-menu",
      ".drawer-panel .site-stats",
    );
    expect(look.link).toEqual(desktop.link);
    // From the left: every line of the menu starts at the left of the menu.
    const lines = await menu.evaluate((nav) => {
      const left = nav.getBoundingClientRect().left;
      const starts = new Map<number, number>();
      for (const link of nav.querySelectorAll("a")) {
        const box = link.getBoundingClientRect();
        const top = Math.round(box.top);
        starts.set(top, Math.min(starts.get(top) ?? Infinity, box.left));
      }
      return [...starts.values()].map((start) => Math.round(start - left));
    });
    expect(lines.length).toBeGreaterThan(1);
    expect(new Set(lines)).toEqual(new Set([0]));
    // The column of the page after it: its blocks, under the menu.
    await expect(
      column.locator(".sidebar-left .sidebar-title").first(),
    ).toBeVisible();
    await phone.context().close();
  });

  test("switches to its right view, led by the statistics of the header, and back", async ({
    page,
    browser,
  }) => {
    await page.setViewportSize(DESKTOP);
    await page.goto("/");
    await expect(page.locator("header .site-stats")).toBeVisible({
      timeout: 15000,
    });
    const desktop = await lookOf(
      page,
      "header nav.top-menu",
      "header .site-stats",
    );

    const phone = await phonePage(browser);
    await phone.goto("/");
    const column = await openColumn(phone);
    await column.getByRole("tab", { name: "О сайте" }).click();
    await expect(column.getByRole("tab", { name: "О сайте" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    const right = column.locator(".sidebar-right");
    await expect(right.locator(".site-stats")).toBeVisible({ timeout: 15000 });
    await expect(
      column.getByRole("navigation", { name: "Основные разделы" }),
    ).toBeHidden();

    const look = await lookOf(
      phone,
      ".drawer-panel nav.main-menu",
      ".drawer-panel .site-stats",
    );
    expect(look.stats).toEqual(desktop.stats);
    expect(look.rows).toBe(desktop.rows);
    // The statistics lead the view, not a block named for them.
    expect(
      await right.evaluate((view) =>
        view.firstElementChild?.classList.contains("site-stats"),
      ),
    ).toBe(true);
    await expect(
      right.locator(".sidebar-title", { hasText: "Статистика сайта" }),
    ).toHaveCount(0);

    await column.getByRole("tab", { name: "Меню" }).click();
    await expect(
      column.getByRole("navigation", { name: "Основные разделы" }),
    ).toBeVisible();
    await expect(right).toBeHidden();
    await phone.context().close();
  });

  test("holds the whole right column, its end included, and closes on the documents", async ({
    browser,
  }) => {
    const page = await phonePage(browser);
    await page.goto("/");
    const column = await openColumn(page);
    await column.getByRole("tab", { name: "О сайте" }).click();
    // The last block of the column is loaded with the rest of it.
    await expect(
      column.locator(".sidebar-title", { hasText: "Правовая информация" }),
    ).toHaveCount(1);
    await expect(
      column.locator(".sidebar-title", { hasText: "Наши соцсети" }),
    ).toHaveCount(1);

    const titles = await column
      .locator(".sidebar-right .sidebar-title")
      .evaluateAll((headings) =>
        headings.map((heading) => (heading.textContent ?? "").trim()),
      );
    expect(titles.filter((title) => TAIL.includes(title))).toEqual(TAIL);
    expect(titles[titles.length - 1]).toBe("Правовая информация");
    expect(await headingsNamed(page, TAIL)).toEqual(
      TAIL.map((name) => ({ name, count: 1, inColumn: true })),
    );
    // Nothing of it under the page: between the page and the footer the
    // shell stands nothing.
    expect(
      await page.evaluate(() => {
        const body = document.querySelector("main#main")!.parentElement!;
        return body.nextElementSibling?.className ?? "";
      }),
    ).toBe("");
    await expect(column.locator(".settings-panel")).toHaveCount(0);
    await expect(
      column.getByRole("link", { name: "Пользовательское соглашение" }),
    ).toHaveAttribute("href", "/agreement");
    await expect(
      column.getByRole("link", { name: "Политика конфиденциальности" }),
    ).toHaveAttribute("href", "/privacy");
    await page.context().close();
  });

  test("takes most of the screen", async ({ browser }) => {
    const page = await phonePage(browser);
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.goto("/");
    const column = await openColumn(page);
    const box = await column.boundingBox();

    expect(box?.x).toBe(0);
    expect(box!.width / LAYOUT_WIDTH).toBeCloseTo(0.85, 1);
    await page.context().close();
  });
});

test.describe("the end of the right column on a desktop", () => {
  test.use({ viewport: DESKTOP });

  test("stays at the end of the right column, each block once", async ({
    page,
  }) => {
    await page.goto("/");
    await expect(
      page.locator(".sidebar-right .sidebar-title", {
        hasText: "Наши соцсети",
      }),
    ).toHaveCount(1);

    expect(await headingsNamed(page, TAIL)).toEqual(
      TAIL.map((name) => ({ name, count: 1, inColumn: false })),
    );
    await expect(
      page.locator(".sidebar-right .sidebar-title", {
        hasText: "Обратная связь",
      }),
    ).toBeVisible();
    // The documents are in the footer of the same screen there.
    await expect(
      page.locator(".sidebar-title", { hasText: "Правовая информация" }),
    ).toHaveCount(0);
  });
});

test.describe("a page a letter leads to", () => {
  test("has no blocks of the columns on a desktop", async ({ page }) => {
    await page.setViewportSize(DESKTOP);
    await page.goto("/reset-password");
    await expect(page.locator("main")).toBeVisible();
    // Whatever the shell would load after the page has been loaded.
    await page.waitForLoadState("networkidle");

    expect(await headingsNamed(page, TAIL)).toEqual(
      TAIL.map((name) => ({ name, count: 0, inColumn: false })),
    );
    await expect(page.locator("aside.sidebar-right")).toHaveCount(0);
    await expect(page.locator("aside.sidebar-left")).toHaveCount(0);
  });

  test("keeps the menu and the statistics alone in the column of a phone", async ({
    browser,
  }) => {
    const page = await phonePage(browser);
    await page.goto("/reset-password");
    await expect(page.locator("main")).toBeVisible();
    await page.waitForLoadState("networkidle");
    const column = await openColumn(page);

    await expect(
      column.getByRole("navigation", { name: "Основные разделы" }),
    ).toBeVisible();
    await column.getByRole("tab", { name: "О сайте" }).click();
    await expect(column.locator(".site-stats")).toBeVisible({
      timeout: 15000,
    });
    await expect(column.locator(".sidebar-title")).toHaveCount(0);
    expect(await headingsNamed(page, TAIL)).toEqual(
      TAIL.map((name) => ({ name, count: 0, inColumn: false })),
    );
    await page.context().close();
  });
});
