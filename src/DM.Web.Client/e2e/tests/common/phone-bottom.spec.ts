import { test as base, expect, type Page } from "@playwright/test";
import { PHONES, phoneContext } from "../../fixtures/phone";

/**
 * The bottom of a page on a phone, as a reader sees it (the owner, 12 October
 * 2026, on the bottom of a phone page: the background of the footer is broken,
 * and "up", "down" and "expand all" are broken and keep disappearing).
 *
 *  - The paper strips of the footer and of the header draw their torn edge at
 *    the height they have on a desktop, 150 and 100, whatever height the
 *    footer or the header takes on a phone.
 *  - The column of buttons at the bottom right stands at every moment: after
 *    the page stops, while a field holds the caret, with the gear of the
 *    settings in it. It used to fade a moment after every scroll.
 *  - It stands in the gutter of the page, right of the text of the page and
 *    left of its scrollbar, so it covers neither. On a page a letter leads to
 *    it stays right of what the card holds.
 *
 * A phone lays the page out as a narrow window (fixtures/phone.ts). Read as a
 * guest: nothing here depends on who looks, and nothing is written.
 */

/** Every page of a test is a phone of the width the describe sets. */
const test = base.extend<{ phoneWidth: number }>({
  phoneWidth: [390, { option: true }],
  page: async ({ browser, phoneWidth }, use) => {
    const context = await phoneContext(browser, {
      width: phoneWidth,
      height: 800,
    });
    await use(await context.newPage());
    await context.close();
  },
});

/** The height of the strip each picture is drawn at on a desktop. */
const FOOTER_EDGE = "auto 150px";
const HEADER_EDGE = "auto 100px";

/**
 * A page long enough to scroll on a phone that asks the stand for nothing:
 * the text of the user agreement is in the bundle.
 */
const LONG_PAGE = "/agreement";

/**
 * A page a letter leads to, the reset of a password. The token travels in the
 * fragment and is checked when the card comes up; the check is answered here
 * as for a live token, so the card holds its form and puts the caret in its
 * field, as it does for the reader who followed the letter.
 */
const LETTER_PAGE = "/reset-password#token=x";

async function openLetterPage(page: Page) {
  await page.route(/\/v1\/account\/password-reset(\?|$)/, (route) =>
    route.request().method() === "GET"
      ? route.fulfill({ json: { status: "ready" } })
      : route.abort(),
  );
  await open(page, LETTER_PAGE);
  await expect(page.locator(".auth-card input").first()).toBeFocused();
}

async function open(page: Page, path: string) {
  await page.goto(path);
  await page.evaluate(() => document.fonts.ready);
  await expect(page.locator(".scroll-nav")).toBeVisible();
}

/** The box of the column of buttons, and whether it is seen and pressable. */
const rail = (page: Page) =>
  page.locator(".scroll-nav").evaluate((el) => {
    const box = el.getBoundingClientRect();
    const style = getComputedStyle(el);
    return {
      left: box.left,
      right: box.right,
      opacity: style.opacity,
      display: style.display,
      pointerEvents: style.pointerEvents,
    };
  });

for (const width of PHONES) {
  test.describe(`The bottom of a page ${width} wide`, () => {
    test.use({ phoneWidth: width });

    test("draws the torn edge of the footer at its desktop height", async ({
      page,
    }) => {
      await open(page, LONG_PAGE);

      const size = await page
        .locator("footer.footer")
        .evaluate((el) => getComputedStyle(el, "::before").backgroundSize);

      expect(size.startsWith(`${FOOTER_EDGE},`), size).toBe(true);
    });

    test("draws the torn edge of the header at its desktop height", async ({
      page,
    }) => {
      await open(page, LONG_PAGE);

      const size = await page
        .locator(".header")
        .first()
        .evaluate((el) => getComputedStyle(el, "::before").backgroundSize);

      expect(size.startsWith(`${HEADER_EDGE},`), size).toBe(true);
    });

    test("keeps the column of buttons up after the page stops", async ({
      page,
    }) => {
      await open(page, LONG_PAGE);
      const main = page.locator(".main");
      const room = await main.evaluate(
        (el) => el.scrollHeight - el.clientHeight,
      );
      expect(room, "the page does not scroll").toBeGreaterThan(300);

      await main.evaluate((el) => el.scrollTo({ top: 300 }));
      // Longer than the column used to wait before it faded.
      await page.waitForTimeout(3000);

      const after = await rail(page);
      expect(after.opacity).toBe("1");
      expect(after.pointerEvents).not.toBe("none");
      await expect(
        page
          .locator(".scroll-nav")
          .getByRole("button", { name: "Настройки сайта" }),
      ).toBeVisible();

      await page
        .locator(".scroll-nav")
        .getByRole("button", { name: "Прокрутить наверх" })
        .click();
      await expect.poll(() => main.evaluate((el) => el.scrollTop)).toBe(0);
    });

    test("stands in the gutter, right of the text and left of the scrollbar", async ({
      page,
    }) => {
      await open(page, LONG_PAGE);

      const edges = await page.evaluate(() => {
        const content = document.querySelector(".content") as HTMLElement;
        const main = document.querySelector(".main") as HTMLElement;
        const box = content.getBoundingClientRect();
        return {
          text: box.right - parseFloat(getComputedStyle(content).paddingRight),
          page: main.getBoundingClientRect().left + main.clientWidth,
        };
      });
      const column = await rail(page);

      expect(column.left, "the column lies on the text").toBeGreaterThanOrEqual(
        edges.text,
      );
      expect(
        column.right,
        "the column lies on the scrollbar",
      ).toBeLessThanOrEqual(edges.page);
    });
  });
}

for (const width of [360, 390]) {
  test.describe(`A page a letter leads to, ${width} wide`, () => {
    test.use({ phoneWidth: width });

    // The column may lie on the frame of the card, never on what it holds.
    test("keeps the column right of what the card holds", async ({ page }) => {
      await openLetterPage(page);

      const inner = await page
        .locator(".auth-card")
        .first()
        .evaluate((el) => {
          const style = getComputedStyle(el);
          return (
            el.getBoundingClientRect().right -
            parseFloat(style.borderRightWidth) -
            parseFloat(style.paddingRight)
          );
        });
      const column = await rail(page);

      expect(column.left).toBeGreaterThan(inner);
      await expect(
        page
          .locator(".scroll-nav")
          .getByRole("button", { name: "Настройки сайта" }),
      ).toBeVisible();
    });

    // The card puts the caret in its field, and the column stays: it used to
    // be taken away while a field held the caret.
    test("keeps the column up while a field holds the caret", async ({
      page,
    }) => {
      await openLetterPage(page);

      const column = await rail(page);
      expect(column.display).not.toBe("none");
      expect(column.opacity).toBe("1");
      await expect(
        page
          .locator(".scroll-nav")
          .getByRole("button", { name: "Прокрутить наверх" }),
      ).toBeVisible();
    });
  });
}
