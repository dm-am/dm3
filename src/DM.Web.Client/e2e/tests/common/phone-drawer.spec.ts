import { test, expect, type Locator, type Page } from "@playwright/test";
import { phonePage } from "../../fixtures/phone";

/**
 * The frame of the sliding column (shared/ui/Drawer/MobileDrawer.vue) and the
 * sign that folds a block of a column (widgets/sidebar/SidebarBlock.vue).
 *
 * The column opens on a bar: the tabs of its two views in the heading style
 * of the site on the left, the cross of a window in the right corner, a line
 * of the theme under them, and the column scrolls under that line while the
 * bar stands. The dimming is the windows' own, with no blur.
 *
 * The sign beside the heading of a block is the plus it was before 12 October
 * (owner, 13 October 2026, on the chevron that replaced it: "нахуя"): a plus
 * on a folded block, turned into a cross on an open one, faintly in sight on a
 * touch screen and under the pointer on a desktop.
 *
 * What the column holds is phone-columns.spec.ts's to check. Everything here
 * is a guest's page that only reads; a fold is kept in the browser and nowhere
 * else.
 */

const DESKTOP = { width: 1366, height: 768 };

/** The bar is the corner of the cross: 32 of the cross and 10 on each side. */
const BAR = 52;
const CROSS = 32;
const CROSS_INSET = 10;

/** The turn of an open block's sign, as the browser writes rotate(45deg). */
const TURNED = "matrix(0.707107, 0.707107, -0.707107, 0.707107, 0, 0)";

/** A colour of the theme, resolved the way the browser resolves a rule. */
function themeColour(page: Page, token: string): Promise<string> {
  return page.evaluate((name) => {
    const probe = document.createElement("span");
    probe.style.color = `var(${name})`;
    document.body.appendChild(probe);
    const colour = getComputedStyle(probe).color;
    probe.remove();
    return colour;
  }, token);
}

/** Opens the column by the button of the header and waits for the slide. */
async function openColumn(page: Page) {
  await page.getByTestId("menu-button").click();
  const column = page.locator(".drawer-panel");
  await expect(column).toBeVisible();
  await expect(column).toHaveCSS("transform", "none");
  return column;
}

const box = async (locator: Locator) => {
  const found = await locator.boundingBox();
  expect(found, "the element has a box on the screen").not.toBeNull();
  return found!;
};

test.describe("the bar of the sliding column", () => {
  test("holds the tabs of the two views over a line, beside the cross of a window", async ({
    browser,
  }) => {
    const page = await phonePage(browser);
    await page.goto("/");
    const column = await openColumn(page);
    const bar = column.locator(".drawer-header");
    const tabs = bar.getByRole("tablist");
    const cross = bar.getByRole("button", { name: "Закрыть" });

    await expect(column).toHaveAccessibleName("Меню");
    await expect(tabs.getByRole("tab")).toHaveText(["Меню", "О сайте"]);
    // The tabs of the site in the heading style: the open one in the ink of
    // a heading, the other greyed, both in capitals.
    const open = tabs.getByRole("tab", { name: "Меню" });
    const other = tabs.getByRole("tab", { name: "О сайте" });
    await expect(open).toHaveAttribute("aria-selected", "true");
    await expect(open).toHaveCSS("text-transform", "uppercase");
    await expect(open).toHaveCSS("color", await themeColour(page, "--heading"));
    await expect(other).toHaveCSS(
      "color",
      await themeColour(page, "--heading-alt"),
    );

    // The bar and the line under it.
    await expect(bar).toHaveCSS("border-bottom-width", "1px");
    await expect(bar).toHaveCSS("border-bottom-style", "solid");
    await expect(bar).toHaveCSS(
      "border-bottom-color",
      await themeColour(page, "--border"),
    );
    expect(await bar.evaluate((el) => el.clientHeight)).toBe(BAR);

    // The cross of a window, drawn and not typed, in the right corner and in
    // the middle of the bar.
    await expect(cross).toHaveText("");
    const barBox = await box(bar);
    const crossBox = await box(cross);
    const tabsBox = await box(tabs);
    expect(crossBox.width).toBeCloseTo(CROSS, 0);
    expect(crossBox.height).toBeCloseTo(CROSS, 0);
    expect(crossBox.y - barBox.y).toBeCloseTo(CROSS_INSET, 0);
    expect(barBox.x + barBox.width - (crossBox.x + crossBox.width)).toBeCloseTo(
      CROSS_INSET,
      0,
    );
    // The tabs on the left, in the gutter of the content, clear of the cross,
    // in the middle of the bar.
    expect(tabsBox.x - barBox.x).toBeCloseTo(16, 0);
    expect(tabsBox.x + tabsBox.width).toBeLessThanOrEqual(crossBox.x);
    expect(
      Math.abs(tabsBox.y + tabsBox.height / 2 - (barBox.y + BAR / 2)),
    ).toBeLessThanOrEqual(1);
    await page.context().close();
  });

  test("dims the page under it without blurring it", async ({ browser }) => {
    const page = await phonePage(browser);
    await page.goto("/");
    await openColumn(page);

    await expect(page.locator(".drawer-scrim")).toHaveCSS(
      "backdrop-filter",
      "none",
    );
    await page.context().close();
  });

  test("keeps its bar still while the column scrolls under the line", async ({
    browser,
  }) => {
    // A guest's column fits whole on a screen of 390 by 844, which shows 1528
    // of the layout in height. A phone of 360 by 640 shows 1255 of it, and
    // there the column runs past the screen and scrolls.
    const page = await phonePage(browser, { width: 360, height: 640 });
    await page.goto("/");
    const column = await openColumn(page);
    const bar = column.locator(".drawer-header");
    const tabs = bar.getByRole("tablist");
    const content = column.locator(".drawer-content");
    // The last block is loaded with the rest, so the column is as long as it
    // will be.
    await expect(column.locator("#sidebar-list-ForumBoards")).toBeAttached();

    await expect(tabs).toBeVisible();
    const before = { bar: await box(bar), tabs: await box(tabs) };
    const lineBottom = before.bar.y + before.bar.height;
    const start = await box(content);
    // The content starts at the line, and its first row a step of 8 under it.
    expect(start.y).toBeCloseTo(lineBottom, 0);
    await expect(content).toHaveCSS("padding-top", "8px");

    const scrolled = await content.evaluate((el) => {
      el.scrollTop = 300;
      return el.scrollTop;
    });
    expect(scrolled, "the column is longer than the screen").toBeGreaterThan(0);

    const after = { bar: await box(bar), tabs: await box(tabs) };
    expect(after).toEqual(before);
    // What scrolls goes under the line, not over the bar.
    expect((await box(content)).y).toBeCloseTo(lineBottom, 0);
    await page.context().close();
  });
});

/**
 * The sign of a block, read off the page: whether it is the plus of two
 * strokes, how it is turned, its size and its ink.
 */
function signOf(toggle: Locator) {
  return toggle.evaluate((button) => {
    const sign = button.querySelector(".icon");
    if (!sign) return null;
    const style = getComputedStyle(sign);
    const stroke = (part: "::before" | "::after") => {
      const drawn = getComputedStyle(sign, part);
      return `${drawn.width} ${drawn.height}`;
    };
    // The box as laid out, before the turn.
    const { offsetWidth: width, offsetHeight: height } = sign as HTMLElement;
    return {
      strokes: [stroke("::before"), stroke("::after")],
      drawn: sign.querySelector("svg") !== null,
      transform: style.transform,
      colour: getComputedStyle(sign, "::before").backgroundColor,
      size: [Math.round(width), Math.round(height)],
    };
  });
}

test.describe("the sign that folds a block, on a touch screen", () => {
  test("is a plus turned into a cross over an open block, faintly at rest", async ({
    browser,
  }) => {
    const page = await phonePage(browser);
    await page.goto("/");
    const column = await openColumn(page);
    const toggle = column.locator("#sidebar-toggle-ForumBoards");
    await expect(toggle).toBeAttached();
    const sign = toggle.locator(".icon");
    const ink = await themeColour(page, "--heading-alt");
    const plus = {
      strokes: ["10px 2px", "2px 10px"],
      drawn: false,
      colour: ink,
      size: [10, 10],
    };

    await expect(toggle).toHaveAttribute("aria-expanded", "true");
    // The turn has its own transition: read once it is over.
    await expect(sign).toHaveCSS("transform", TURNED);
    expect(await signOf(toggle)).toEqual({ ...plus, transform: TURNED });
    // A touch screen has no hover: the sign stays faintly in sight.
    await expect(sign).toHaveCSS("opacity", "0.4");

    await toggle.tap();

    await expect(toggle).toHaveAttribute("aria-expanded", "false");
    await expect(sign).toHaveCSS("transform", "matrix(1, 0, 0, 1, 0, 0)");
    expect((await signOf(toggle))?.strokes).toEqual(plus.strokes);
    await page.context().close();
  });
});

test.describe("the sign that folds a block, under a pointer", () => {
  test.use({ viewport: DESKTOP });

  test("comes up as the plus when the pointer is over the heading", async ({
    page,
  }) => {
    await page.goto("/");
    const toggle = page.locator(".sidebar-left #sidebar-toggle-ForumBoards");
    await expect(toggle).toBeVisible();
    const sign = toggle.locator(".icon");
    // `has` looks inside the heading, so it names the toggle from there.
    const heading = page
      .locator(".sidebar-left .sidebar-title")
      .filter({ has: page.locator("#sidebar-toggle-ForumBoards") });

    await expect(sign).toHaveCSS("opacity", "0");
    await heading.hover();
    await expect(sign).toHaveCSS("opacity", "1");
    await expect(sign).toHaveCSS("transform", TURNED);
    expect(await signOf(toggle)).toEqual({
      strokes: ["10px 2px", "2px 10px"],
      drawn: false,
      transform: TURNED,
      colour: await themeColour(page, "--heading-alt"),
      size: [10, 10],
    });

    await toggle.click();

    await expect(toggle).toHaveAttribute("aria-expanded", "false");
    await expect(sign).toHaveCSS("transform", "matrix(1, 0, 0, 1, 0, 0)");
  });
});

/** The line of every row of a list that holds a link. */
function linesOf(list: Locator) {
  return list.evaluate((ul) =>
    [...ul.children]
      .filter((li) => li.tagName === "LI" && li.querySelector("a"))
      .map((li) => getComputedStyle(li).lineHeight),
  );
}

/** Waits until a block of a column holds its rows, not their skeleton. */
async function loadedRows(column: Locator, token: string) {
  const list = column.locator(`#sidebar-list-${token}`);
  await expect(list.locator(":scope > li a").first()).toBeAttached({
    timeout: 15000,
  });
  return list;
}

// The blocks inside the column look as the columns of the full version: the
// rows keep the line of the page's text, as on 1366 (owner, 13 October 2026:
// the phone is the full version, the reader zooms in).
test.describe("the rows of the sliding column", () => {
  test("keep the line of the desktop's columns", async ({ browser, page }) => {
    await page.setViewportSize(DESKTOP);
    await page.goto("/");
    const desktop = [
      ...(await linesOf(
        await loadedRows(page.locator("aside.sidebar-left"), "ForumBoards"),
      )),
      ...(await linesOf(
        await loadedRows(page.locator("aside.sidebar-right"), "PopularGames"),
      )),
    ];

    const phone = await phonePage(browser);
    await phone.goto("/");
    const column = await openColumn(phone);
    const left = await linesOf(await loadedRows(column, "ForumBoards"));
    await column.getByRole("tab", { name: "О сайте" }).click();
    const right = await linesOf(await loadedRows(column, "PopularGames"));

    expect(desktop.length).toBeGreaterThan(0);
    expect(new Set([...left, ...right])).toEqual(new Set(desktop));
    await phone.context().close();
  });
});
