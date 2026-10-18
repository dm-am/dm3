import {
  test as base,
  expect,
  PRIMARY_STORAGE_STATE,
} from "../../fixtures/auth";
import type { Page } from "@playwright/test";

/**
 * The table of the rooms in the settings of a game keeps the name of every
 * room whole in its field, as the old site's field of 186px does, and stays
 * inside its block.
 *
 * Six columns stand beside the name, and the field gets what they leave: with
 * the cells a step in from their sides, as tight as the old site's, it is
 * about as wide as the old site's field. A layout fact: jsdom lays nothing
 * out, so the unit spec cannot hold it.
 *
 * Read-only: the primary seeded account masters the seeded game, and nothing
 * is saved.
 */
const test = base.extend<{ master: Page }>({
  master: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: PRIMARY_STORAGE_STATE,
      viewport: { width: 1366, height: 900 },
    });
    await use(await context.newPage());
    await context.close();
  },
});

/** The seeded game, the primary account its master. */
const GAME = "aaaaa";

test("keeps the name of every room whole in its field", async ({ master }) => {
  await master.goto(`/game/${GAME}/settings`);
  const table = master.locator(".rooms-table");
  await expect(table.locator(".col-title input").first()).toBeVisible();

  const fit = await table.evaluate((t) => {
    const section = t.closest(".settings-section") as HTMLElement;
    const style = getComputedStyle(section);
    const inner =
      section.clientWidth -
      parseFloat(style.paddingLeft) -
      parseFloat(style.paddingRight);
    const fields = [
      ...t.querySelectorAll<HTMLInputElement>(".col-title input"),
    ];
    return {
      table: t.getBoundingClientRect().width,
      inner,
      longest: Math.max(...fields.map((field) => field.value.length)),
      cut: fields
        .filter((field) => field.scrollWidth > field.clientWidth)
        .map((field) => field.value),
    };
  });

  // Not vacuous: the seed names a room as long as the old site's field holds.
  expect(fit.longest, "the seed holds a long room name").toBeGreaterThan(18);
  expect(fit.cut, "names cut inside their fields").toEqual([]);
  expect(fit.table).toBeLessThanOrEqual(fit.inner + 0.5);
});
