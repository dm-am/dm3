import { test, expect, API_BASE_URL } from "../../fixtures/auth";

/**
 * A closed room in the menu of a game, in front of somebody who sees the game
 * and may not open the room: its title, " - " and the characters let in to
 * write in it, as the old site writes "[Лес] - Густав, Эодор", and a line too
 * long for the column goes on under the start of the menu line, not under the
 * room.
 *
 * The unit specs hold the row and the stylesheet. What only the stand answers:
 * that the names come through the API to a guest at all (the service clears a
 * closed room of everything else), and that the wrap the stylesheet asks for is
 * where a browser puts the second line.
 *
 * Read-only: a guest reads the seeded game and nothing is written.
 */

/** The seeded game the menu is read against, with two closed rooms. */
const GAME = "aaaaa";

interface ListedRoom {
  title: string;
  canView?: boolean;
  isArchived?: boolean;
  characterNames?: string[];
}

test("writes a closed room with the characters let in to write in it, wrapped under the menu line", async ({
  page,
  request,
}) => {
  const response = await request.get(`${API_BASE_URL}/v1/games/${GAME}/rooms`);
  expect(response.ok()).toBe(true);
  const rooms = ((await response.json()) as { resources: ListedRoom[] })
    .resources;
  const closed = rooms.find(
    (room) =>
      room.canView === false &&
      !room.isArchived &&
      (room.characterNames?.length ?? 0) > 0,
  );
  expect(
    closed,
    "the seed lets characters write in a closed room of the game",
  ).toBeTruthy();
  const line = `${closed!.title} - ${closed!.characterNames!.join(", ")}`;

  await page.goto(`/game/${GAME}`);
  const row = page.locator(".room-list li", { hasText: closed!.title });
  await expect(row).toHaveText(line);
  await expect(row.locator("a")).toHaveCount(0);

  // The lines the row takes in the column: the first starts one indent in,
  // past the start of the menu line, and every next one at that start.
  const layout = await row.evaluate((li) => {
    const range = document.createRange();
    range.selectNodeContents(li);
    const rects = [...range.getClientRects()].filter((r) => r.width > 0);
    const top = Math.min(...rects.map((r) => r.top));
    const firstLeft = Math.min(
      ...rects.filter((r) => r.top - top < 4).map((r) => r.left),
    );
    const nextLefts = rects.filter((r) => r.top - top >= 4).map((r) => r.left);
    const menuLine = li.closest(".room-list")!.getBoundingClientRect().left;
    return { firstLeft, nextLefts, menuLine };
  });
  expect(
    layout.nextLefts.length,
    "the line is longer than the column and wraps",
  ).toBeGreaterThan(0);
  expect(layout.firstLeft).toBeGreaterThan(layout.menuLine + 4);
  expect(Math.min(...layout.nextLefts)).toBeCloseTo(layout.menuLine, 0);
});
