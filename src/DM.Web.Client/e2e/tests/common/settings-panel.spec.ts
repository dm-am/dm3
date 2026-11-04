import { test, expect, type Page } from "@playwright/test";
import { settle } from "../../fixtures/window";

/**
 * The settings bubble hangs off the right edge of the viewport, so its width is
 * its position: every pixel it gains moves its left edge, its labels and the
 * controls of every other row. So the controls column is declared, not
 * discovered: a fixed track, and no control wider than it.
 */

/** SiteSettingsPanel.vue, $settings-control-width. Changing one means changing both. */
const CONTROL_COLUMN = 64;

async function openSettings(page: Page) {
  await page.goto("/");

  // The label column is sized by its text and PT Sans loads with
  // font-display: swap, so until the face arrives the labels are measured in
  // the fallback and the panel is a different width. Measuring across that
  // moment compares two layouts and fails on neither of them.
  await settle(page);

  await page
    .locator(".scroll-nav")
    .getByRole("button", { name: "Настройки сайта" })
    .click();
  // Scoped to the panel: the class is shared with SpeechBubble.
  const bubble = page.locator(".scroll-nav .bubble-content");
  await expect(bubble).toBeVisible();
  return bubble;
}

test.describe("Settings panel geometry", () => {
  // Pinned, so that every browser profile measures the same layout.
  test.use({ viewport: { width: 1920, height: 911 } });

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
