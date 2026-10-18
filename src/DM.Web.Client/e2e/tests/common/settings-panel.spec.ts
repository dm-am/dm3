import { test, expect, type Page } from "@playwright/test";
import { phonePage } from "../../fixtures/phone";

/**
 * The settings bubble hangs off the right edge of the viewport, so its width is
 * its position: every pixel it gains moves its left edge, its labels and the
 * controls of every other row. That width used to be decided by whichever
 * control happened to be widest, which is a coupling nobody can see in the
 * markup and nobody thinks about when adding a row. One segmented switch,
 * added to a row that has since been removed again, shifted every row in the
 * panel.
 *
 * So the controls column is declared, not discovered, and this file is what
 * holds the declaration to its word. It fails on the two ways the old coupling
 * can come back: a track that is no longer fixed, and a control that outgrows
 * it.
 */

/** SiteSettingsPanel.vue, $settings-control-width. Changing one means changing both. */
const CONTROL_COLUMN = 64;

async function openSettings(page: Page) {
  await page.goto("/");

  // The label column is sized by its text and PT Sans loads with
  // font-display: swap, so until the face arrives the labels are measured in
  // the fallback and the panel is a different width. Measuring across that
  // moment compares two layouts and fails on neither of them.
  await page.evaluate(() => document.fonts.ready);

  await page
    .locator(".scroll-nav")
    .getByRole("button", { name: "Настройки сайта" })
    .click();
  // Scoped to the panel that owns it. The class is shared with SpeechBubble,
  // which the same pages also draw, so an unscoped locator matched two elements
  // and the strict-mode violation read as a broken panel rather than as an
  // under-specified test.
  const bubble = page.locator(".scroll-nav .bubble-content");
  await expect(bubble).toBeVisible();
  return bubble;
}

test.describe("Settings panel geometry", () => {
  // Pinned, so that every browser profile measures the same desktop layout;
  // the phone has a describe of its own at the end of this file.
  test.use({ viewport: { width: 1280, height: 720 } });

  test("the controls column is a fixed track, not the widest control", async ({
    page,
  }) => {
    const bubble = await openSettings(page);

    const tracks = await bubble.evaluate(
      (el) => getComputedStyle(el).gridTemplateColumns,
    );

    expect(
      tracks.split(" ")[1],
      "the second track is the controls column and it is declared in pixels",
    ).toBe(`${CONTROL_COLUMN}px`);
  });

  test("no control is wider than the column it sits in", async ({ page }) => {
    await openSettings(page);

    const overflowing = await page.locator(".settings-row").evaluateAll(
      (rows, column) =>
        rows
          .map((row) => {
            const control = row.lastElementChild as HTMLElement | null;
            if (!control) return null;
            const width = control.getBoundingClientRect().width;
            const label = row.querySelector(".settings-label")?.textContent;
            // Half a pixel of tolerance: layout rounds, contracts do not.
            return width > column + 0.5
              ? `${label?.trim()}: ${width.toFixed(1)}px`
              : null;
          })
          .filter(Boolean),
      CONTROL_COLUMN,
    );

    expect(
      overflowing,
      "a control wider than the column pushes the whole panel sideways",
    ).toEqual([]);
  });

  // The owner's word: the switches were not centred under the layout toggle.
  // Every control stands in the middle of the column, so the centre of each
  // switch is the centre of the toggle above it.
  test("every control is centred under the layout toggle", async ({ page }) => {
    await openSettings(page);

    const centres = await page.locator(".settings-row").evaluateAll((rows) =>
      rows.map((row) => {
        const box = (
          row.lastElementChild as HTMLElement
        ).getBoundingClientRect();
        return Math.round(box.left + box.width / 2);
      }),
    );

    expect(new Set(centres).size, centres.join(", ")).toBe(1);
  });

  // The switch hides its input at no size; the ring goes round what is seen.
  test("rings the switch the caret stands on", async ({ page }) => {
    await openSettings(page);
    const input = page.getByRole("checkbox", { name: "Перенос слов" });
    await input.focus();
    await page.keyboard.press("Shift+Tab");
    await page.keyboard.press("Tab");

    const ring = await input.evaluate((el) => {
      const slider = el.nextElementSibling as HTMLElement;
      const style = getComputedStyle(slider);
      return `${style.outlineStyle} ${style.outlineWidth}`;
    });

    expect(ring).toBe("solid 2px");
  });

  test("hiding the widest control leaves the panel where it was", async ({
    page,
  }) => {
    const bubble = await openSettings(page);

    const before = await bubble.boundingBox();

    // The mechanism, not the symptom: with an auto track this alone changed
    // the width of the panel and the position of every label in it.
    //
    // !important because the rule has to beat the component's own: a scoped
    // style carries a data attribute and therefore more specificity than a
    // bare class, so without it this injection changed nothing and the test
    // passed on the very layout it exists to refuse.
    await page.addStyleTag({
      content: ".layout-toggle, .settings-switch { display: none !important }",
    });

    const after = await bubble.boundingBox();

    expect(after?.width).toBeCloseTo(before?.width ?? 0, 1);
    expect(after?.x).toBeCloseTo(before?.x ?? 0, 1);
  });
});

/**
 * On a phone the gear stands in the floating column as it does on a desktop,
 * and the rows open in its bubble to the left of the column, not in the right
 * column, where the settings have no place. The column stands 4 from the edge
 * of the layout, so the bubble hanging off it has to stand whole inside the
 * window. A phone lays the page out at the width of a narrow window, whatever
 * the width of its screen (fixtures/phone.ts): the window is read off the page.
 */
for (const width of [320, 360, 390]) {
  test.describe(`Settings panel on a phone ${width} wide`, () => {
    let page: Page;
    test.beforeEach(async ({ browser }) => {
      page = await phonePage(browser, { width, height: 800 });
    });
    test.afterEach(async () => {
      await page.context().close();
    });

    test("opens from the gear and stands whole inside the window", async () => {
      const bubble = await openSettings(page);

      const box = await bubble.boundingBox();
      const window = await page.evaluate(() => ({
        width: innerWidth,
        height: innerHeight,
      }));
      expect(box, "the bubble is not drawn").not.toBeNull();
      expect(box!.x, "off the left edge").toBeGreaterThanOrEqual(0);
      expect(box!.x + box!.width, "off the right edge").toBeLessThanOrEqual(
        window.width,
      );
      expect(box!.y, "off the top").toBeGreaterThanOrEqual(0);
      expect(box!.y + box!.height, "off the bottom").toBeLessThanOrEqual(
        window.height,
      );
      await expect(bubble.locator(".settings-label")).toHaveText([
        "Верстка",
        "Тема",
        "Перенос слов",
      ]);
    });

    test("keeps every control whole inside the bubble", async () => {
      const bubble = await openSettings(page);

      const outside = await bubble.evaluate((root) => {
        const box = root.getBoundingClientRect();
        return [...root.querySelectorAll(".settings-row")]
          .map((row) => {
            const control = (
              row.lastElementChild as HTMLElement
            ).getBoundingClientRect();
            const label = row
              .querySelector(".settings-label")
              ?.textContent?.trim();
            return control.left < box.left || control.right > box.right + 0.5
              ? label
              : null;
          })
          .filter(Boolean);
      });

      expect(outside, "a control sticks out of the bubble").toEqual([]);
    });

    // A guest's theme is kept on the device: nothing is written to the stand.
    test("turns the theme from a press on the word of its row", async () => {
      const bubble = await openSettings(page);
      const dark = () =>
        page.evaluate(() =>
          document.documentElement.classList.contains("theme_Dark"),
        );
      const before = await dark();

      await bubble.locator(".settings-label", { hasText: "Тема" }).click();

      await expect.poll(dark).toBe(!before);
      await expect
        .poll(() => page.evaluate(() => localStorage.getItem("dm_theme")))
        .toBe(before ? "Light" : "Dark");
    });
  });
}
