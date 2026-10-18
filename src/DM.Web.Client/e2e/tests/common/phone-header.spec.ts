import {
  test as base,
  expect,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";
import {
  LAYOUT_WIDTH,
  PHONES,
  pastTheLayout,
  phoneContext,
} from "../../fixtures/phone";
import type { BrowserContext, Page } from "@playwright/test";

/**
 * The header as a reader of a phone and of a narrow window sees it
 * (UI_STANDARDS, "Шапка").
 *
 * A phone lays the page out as wide as index.html says, the layout of a
 * narrow window, and shows it whole, smaller (owner, 13 October 2026: no
 * mobile version, the reader zooms in). Up to the shell breakpoint the header
 * has one button, at the left edge, in the middle of the logo and the line
 * under it, and the logo in the middle of the screen; the menu and the
 * statistics are in the sliding column the button brings in
 * (phone-columns.spec.ts). The sign of the button is the sign of the compact
 * layout of the site settings with one bar fewer, in the ink of the text.
 *
 * The greeting under the logo is the whole name at every width. A name too
 * long for the line wraps between the words of the greeting, a name wider
 * than the line breaks at its edge, and nothing goes under "…" or past the
 * box of the line: a login is up to 20 letters of any alphabet
 * (UsernamePolicy), wide ones included. What is read is the text as it is
 * laid out, letter boxes against the box of the line; the field a finger
 * finds a count by reaches past the line on purpose and is not text.
 */

/** The drawing of the logo on 1366: 55 high in the proportions of logo.svg. */
const LOGO_1366 = (55 * 4079.6) / 827.08;

/** Logins a line under the logo cannot hold in one line. */
const LONG_LOGINS = {
  latin: "W".repeat(20),
  cyrillic: "Ш".repeat(20),
  fullwidth: "Ｗ".repeat(20),
};

type Role = "guest" | "user";

/** A phone by the width of its screen, or a window of a desktop. */
type Screen = { phone: number } | { window: number };

const named = (screen: Screen) =>
  "phone" in screen
    ? `a phone of ${screen.phone}`
    : `a window of ${screen.window}`;

const test = base.extend<{
  /** Opens the home page as a role on a screen, under another login if given. */
  openAs: (
    role: Role,
    screen: Screen,
    options?: { login?: string },
  ) => Promise<Page>;
}>({
  openAs: async ({ browser }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (role, screen, options = {}) => {
      const storageState =
        role === "user" ? SECONDARY_STORAGE_STATE : undefined;
      const context =
        "phone" in screen
          ? await phoneContext(browser, {
              storageState,
              width: screen.phone,
            })
          : await browser.newContext({
              storageState,
              viewport: { width: screen.window, height: 800 },
            });
      contexts.push(context);
      const login = options.login;
      if (login) {
        // The reader's own name, as the page keeps it and as the server
        // answers it: nothing reaches the server under the other name.
        await context.addInitScript((name) => {
          const kept = localStorage.getItem("user");
          if (!kept) return;
          localStorage.setItem(
            "user",
            JSON.stringify({ ...JSON.parse(kept), username: name }),
          );
        }, login);
        await context.route(/\/v1\/users\/me\/profile(\?|$)/, async (route) => {
          if (route.request().method() !== "GET") return route.continue();
          const response = await route.fetch();
          const body = await response.json();
          const user = body.resource ?? body;
          user.username = login;
          await route.fulfill({ response, json: body });
        });
      }
      const page = await context.newPage();
      await page.goto("/");
      await expect(page.locator("header.header .logo")).toBeVisible({
        timeout: 15000,
      });
      if (role === "user")
        await expect(page.getByTestId("user-menu")).toHaveText(login ?? /\S/, {
          timeout: 15000,
        });
      await page.waitForLoadState("networkidle");
      await page.evaluate(() => document.fonts.ready);
      return page;
    });
    for (const context of contexts) await context.close();
  },
});

interface Box {
  left: number;
  right: number;
  top: number;
  bottom: number;
  width: number;
  height: number;
}

/** The boxes of a narrow header, and the ink of its button. */
function readHeader(page: Page) {
  return page.evaluate(() => {
    const box = (selector: string): Box => {
      const el = document.querySelector(selector);
      if (!el) throw new Error(`no ${selector}`);
      const { left, right, top, bottom, width, height } =
        el.getBoundingClientRect();
      return { left, right, top, bottom, width, height };
    };
    const button = "[data-testid='menu-button']";
    // The ink of the text, as the browser resolves the theme's variable.
    const probe = document.createElement("span");
    probe.style.color = "var(--text)";
    document.body.appendChild(probe);
    const text = getComputedStyle(probe).color;
    probe.remove();
    return {
      header: box("header.header"),
      logo: box("header.header .logo"),
      line: box("header.header .user-actions > *"),
      button: box(button),
      sign: box(`${button} svg`),
      bars: document.querySelectorAll(`${button} svg rect`).length,
      text,
      ink: getComputedStyle(document.querySelector(button)!).color,
      buttons: document.querySelectorAll(
        "header.header button[aria-haspopup='dialog']",
      ).length,
      menu: document.querySelectorAll("header.header nav.top-menu").length,
      statistics: document.querySelectorAll("header.header .site-stats").length,
    };
  });
}

/**
 * The greeting as it is laid out: the box of its line, every box of its text
 * (letters and inline boxes, not the touch fields of the counts), whether
 * anything in the line clips its text or draws "…", and whether the page or
 * the shell's scroller runs past the window.
 */
function readGreeting(page: Page) {
  return page.evaluate(() => {
    const line = document.querySelector<HTMLElement>("header .user-actions")!;
    const greeting = document.querySelector<HTMLElement>(
      "[data-testid='greeting']",
    )!;
    const range = document.createRange();
    range.selectNodeContents(greeting);
    const boxes = Array.from(range.getClientRects())
      .filter((r) => r.width > 0 && r.height > 0)
      .map((r) => ({
        left: r.left,
        right: r.right,
        top: Math.round(r.top),
      }));
    const cut = [greeting, ...greeting.querySelectorAll<HTMLElement>("*")]
      .filter((el) => {
        const style = getComputedStyle(el);
        if (style.textOverflow === "ellipsis") return true;
        return (
          style.overflowX !== "visible" && el.scrollWidth > el.clientWidth + 1
        );
      })
      .map((el) => el.className || el.tagName);
    const own = line.getBoundingClientRect();
    const sideways = [
      document.scrollingElement,
      document.querySelector(".main"),
    ]
      .filter((el): el is Element => !!el)
      .filter((el) => el.scrollWidth > el.clientWidth + 1)
      .map((el) => `${el.className || el.tagName}: ${el.scrollWidth}`);
    return {
      name: document.querySelector("[data-testid='user-menu']")!.textContent,
      line: { left: own.left, right: own.right },
      boxes,
      lines: new Set(boxes.map((b) => b.top)).size,
      text: greeting.textContent ?? "",
      cut,
      sideways,
      header: document.querySelector("header.header")!.getBoundingClientRect()
        .height,
    };
  });
}

const middle = (b: Box) => b.top + b.height / 2;

test.describe("The page of a phone", () => {
  for (const width of PHONES) {
    test(`is laid out ${LAYOUT_WIDTH} wide on a phone of ${width}, nothing past it`, async ({
      openAs,
    }) => {
      const page = await openAs("user", { phone: width });

      expect(await page.evaluate(() => window.innerWidth)).toBe(LAYOUT_WIDTH);
      expect(await page.evaluate(pastTheLayout, LAYOUT_WIDTH)).toEqual([]);
    });
  }
});

test.describe("The header up to the shell breakpoint", () => {
  const SCREENS: Screen[] = [
    ...PHONES.map((phone) => ({ phone })),
    { window: 768 },
    { window: 1000 },
  ];

  for (const screen of SCREENS) {
    for (const role of ["guest", "user"] as const) {
      test(`has one button, in the middle of the logo and its line, on ${named(screen)} as ${role}`, async ({
        openAs,
      }) => {
        const page = await openAs(role, screen);
        const h = await readHeader(page);

        // One button, and nothing of the header of a desktop.
        expect(h.buttons).toBe(1);
        expect(h.menu).toBe(0);
        expect(h.statistics).toBe(0);
        await expect(page.getByTestId("menu-button")).toHaveAccessibleName(
          "Меню",
        );
        // At the left edge, 8 from it.
        expect(Math.round(h.button.left - h.header.left)).toBe(8);
        // Down, in the middle of the logo and the line under it as one block.
        const block = (h.logo.top + h.line.bottom) / 2;
        expect(
          Math.abs(middle(h.button) - block),
          `${middle(h.button)} and ${block}`,
        ).toBeLessThanOrEqual(1);
        // The logo of 1366 in the middle of the header, the width of the page.
        expect(
          Math.abs(
            (h.logo.left + h.logo.right) / 2 -
              (h.header.left + h.header.right) / 2,
          ),
        ).toBeLessThanOrEqual(1);
        expect(Math.abs(h.logo.width - LOGO_1366)).toBeLessThanOrEqual(1);
      });
    }

    test(`draws the sign of the button in the ink of the text, three bars 22 across, on ${named(screen)}`, async ({
      openAs,
    }) => {
      const page = await openAs("user", screen);
      const h = await readHeader(page);

      expect(h.ink).toBe(h.text);
      expect(h.bars).toBe(3);
      expect(Math.round(h.sign.width)).toBe(22);
      expect(h.sign.height).toBeCloseTo(16.5, 0);
      expect(Math.round(h.button.width)).toBe(44);
      expect(Math.round(h.button.height)).toBe(44);
    });
  }
});

test.describe("The greeting of a long name", () => {
  const SCREENS: Screen[] = [{ phone: 360 }, { phone: 390 }, { window: 1366 }];

  for (const screen of SCREENS) {
    for (const [kind, login] of Object.entries(LONG_LOGINS)) {
      test(`shows a ${kind} login of 20 whole inside its line on ${named(screen)}`, async ({
        openAs,
      }) => {
        const page = await openAs("user", screen, { login });
        const g = await readGreeting(page);

        expect(g.name).toBe(login);
        expect(g.text).not.toContain("…");
        expect(g.cut).toEqual([]);
        const outside = g.boxes.filter(
          (b) => b.left < g.line.left - 1 || b.right > g.line.right + 1,
        );
        expect(outside, `line ${g.line.left}..${g.line.right}`).toEqual([]);
        expect(g.sideways).toEqual([]);
      });
    }
  }

  test("leaves a name that fits in one line where it was at 1366px", async ({
    openAs,
  }) => {
    const page = await openAs(
      "user",
      { window: 1366 },
      { login: "LongestLoginPossible" },
    );
    const g = await readGreeting(page);

    expect(g.name).toBe("LongestLoginPossible");
    expect(g.cut).toEqual([]);
    expect(g.lines).toBe(1);
    expect(g.header).toBe(100);
  });
});
