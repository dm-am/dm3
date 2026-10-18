import { test, expect, SECONDARY_STORAGE_STATE } from "../../fixtures/auth";

/**
 * The table of the attributes of a sheet keeps all six of its columns at 1366
 * in the two narrowest places it stands: the form of a new game (a box of 600)
 * and the settings of a game (610). Its cells are 4 apart at their sides for
 * that; at 8 the least width of the six is 626 and the table scrolls.
 *
 * Read as TestUser, the master of the game "aaaam" in the seed. Nothing is
 * written: the pages are only opened.
 */

test.use({
  storageState: SECONDARY_STORAGE_STATE,
  viewport: { width: 1366, height: 900 },
});

const HEADERS = [
  "Название",
  "Тип",
  "Приватность",
  "Обязательность",
  "Дескриптор",
  "",
];

for (const [name, path] of [
  ["в форме новой игры", "/games/create"],
  ["в настройках игры", "/game/aaaam/settings"],
] as const) {
  test(`на 1366 ${name} у схемы атрибутов стоят все шесть столбцов без прокрутки`, async ({
    page,
  }) => {
    await page.goto(path);
    const table = page.locator("main#main .attr-table");
    await expect(table.locator("tbody tr.attr-row").first()).toBeVisible({
      timeout: 60_000,
    });
    await page.evaluate(() => document.fonts.ready);

    const headers = table.locator("thead th");
    await expect(headers).toHaveCount(HEADERS.length);
    for (const [index, header] of HEADERS.entries()) {
      await expect(headers.nth(index)).toBeVisible();
      await expect(headers.nth(index)).toHaveText(header);
    }
    const box = await page
      .locator("main#main .schema-editor .table-wrap")
      .evaluate((wrap) => ({
        scroll: wrap.scrollWidth,
        client: wrap.clientWidth,
      }));
    expect(box.scroll, "the table scrolls in its box").toBeLessThanOrEqual(
      box.client,
    );
  });
}
