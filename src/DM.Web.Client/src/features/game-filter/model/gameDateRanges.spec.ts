/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, afterEach } from "vitest";
import { filterOnListPage, type Page } from "@/shared/lib/testing/urlFilter";
import { GAME_DATE_RANGES } from "./gameDateRanges";
import { createDefaultState } from "./gamesFilterReducer";
import { useGamesFilter } from "./useGamesFilter";

let page: Page | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function filter() {
  const on = await filterOnListPage(useGamesFilter, {});
  page = on;
  return on;
}

describe("GAME_DATE_RANGES", () => {
  it("names each date field of the filter state once", () => {
    const named = GAME_DATE_RANGES.flatMap((range) => [range.from, range.to]);
    const held = Object.keys(createDefaultState()).filter((key) =>
      key.endsWith("Utc"),
    );

    expect([...named].sort()).toEqual([...held].sort());
    expect(new Set(named).size).toBe(named.length);
  });

  it("names each range setter of the filter once", async () => {
    const on = await filter();
    const setters = Object.keys(on.filter).filter((key) =>
      /^set\w+Range$/.test(key),
    );

    expect(GAME_DATE_RANGES.map((range) => range.setter).sort()).toEqual(
      setters.sort(),
    );
  });

  it.each(GAME_DATE_RANGES.map((range) => ({ ...range })))(
    "writes $key through its setter into its own fields",
    async (range) => {
      const on = await filter();

      on.filter[range.setter]("2026-01-01", "2026-01-02");

      expect(await on.settled()).toEqual({
        [range.from]: "2026-01-01",
        [range.to]: "2026-01-02",
      });
    },
  );
});
