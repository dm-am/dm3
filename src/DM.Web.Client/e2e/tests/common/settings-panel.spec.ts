import { test, expect } from "@playwright/test";

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

/** ScrollNav.vue, $settings-control-width. Changing one means changing both. */
const CONTROL_COLUMN = 64;

async function openSettings(page: import("@playwright/test").Page) {
  await page.goto("/");

  // The label column is sized by its text and PT Sans loads with
  // font-display: swap, so until the face arrives the labels are measured in
  // the fallback and the panel is a different width. Measuring across that
  // moment compares two layouts and fails on neither of them.
  await page.evaluate(() => document.fonts.ready);

  await page.getByRole("button", { name: "Настройки сайта" }).click();
  const bubble = page.locator(".bubble-content");
  await expect(bubble).toBeVisible();
  return bubble;
}

test.describe("Settings panel geometry", () => {
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
      content: ".layout-toggle, .theme-switch { display: none !important }",
    });

    const after = await bubble.boundingBox();

    expect(after?.width).toBeCloseTo(before?.width ?? 0, 1);
    expect(after?.x).toBeCloseTo(before?.x ?? 0, 1);
  });
});
