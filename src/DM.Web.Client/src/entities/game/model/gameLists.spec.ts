/**
 * @vitest-environment jsdom
 */

/**
 * A game list of the left column reads the rows of its block. Widened, it asks
 * the server for the rows named, though the copy it holds is fresh for 30 s.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { Api } from "@/shared/api";
import { createGameLists } from "./gameLists";
import { SIDEBAR_GAME_ROWS } from "./sidebarRows";

type Lists = ReturnType<typeof createGameLists>["lists"];

const LISTS = [
  {
    name: "Активные игры",
    rows: SIDEBAR_GAME_ROWS.active,
    of: (l: Lists) =>
      [l.fetchActiveGames, l.widenActiveGames, l.activeGamesTake] as const,
  },
  {
    name: "Набор игроков",
    rows: SIDEBAR_GAME_ROWS.recruiting,
    of: (l: Lists) =>
      [
        l.fetchRecruitingGames,
        l.widenRecruitingGames,
        l.recruitingGamesTake,
      ] as const,
  },
  {
    name: "Завершенные игры",
    rows: SIDEBAR_GAME_ROWS.finished,
    of: (l: Lists) =>
      [
        l.fetchFinishedGames,
        l.widenFinishedGames,
        l.finishedGamesTake,
      ] as const,
  },
];

afterEach(() => {
  vi.restoreAllMocks();
});

describe("a game list of the left column", () => {
  it.each(LISTS)(
    "$name asks for the rows it is widened to, past its fresh copy",
    async ({ rows, of }) => {
      const get = vi
        .spyOn(Api, "get")
        .mockResolvedValue({ data: { resources: [] }, error: null } as never);
      const [read, widen, take] = of(createGameLists().lists);

      await read();
      await widen(rows + 3);

      expect(get.mock.calls.map(([, query]) => query?.take)).toEqual([
        rows,
        rows + 3,
      ]);
      expect(take.value).toBe(rows + 3);
    },
  );
});
