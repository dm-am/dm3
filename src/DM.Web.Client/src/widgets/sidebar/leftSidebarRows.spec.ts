/**
 * The left column shows a game or a blog once: a block leaves out what a block
 * above it shows, and keeps its length by asking the server for more.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { effectScope, nextTick, ref } from "vue";
import { createPinia, setActivePinia } from "pinia";
import {
  GameParticipation,
  GameStatus,
  SIDEBAR_GAME_ROWS,
  useGamesStore,
  type GameRef,
} from "@/entities/game";
import { useBlogsStore, type BlogRef } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/common";
import { MAX_API_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import {
  useFilledRows,
  useLeftSidebarBlogs,
  useLeftSidebarGames,
} from "./leftSidebarRows";

const game = (
  id: string,
  participation: GameParticipation[] = [],
  status: GameStatus = GameStatus.Active,
) => ({ id, title: `Игра ${id}`, status, participation }) as unknown as GameRef;

const blog = (id: string) =>
  ({ id, title: `Блог ${id}`, status: "Active" }) as unknown as BlogRef;

const idsOf = (rows: readonly { id: string }[] | null) =>
  rows?.map((row) => row.id) ?? null;

function signIn(role = UserRole.RegularUser) {
  useAuthStore().user = { username: "viewer", role } as never;
}

beforeEach(() => {
  setActivePinia(createPinia());
});

describe("the games of the left column", () => {
  it("leaves out of Набор игроков and Завершенные игры the games of Мои игры", () => {
    signIn();
    const store = useGamesStore();
    store.participatingGames = [
      game("played", [GameParticipation.Player]),
      game("read", [GameParticipation.Reader], GameStatus.Closed),
    ];
    store.recruitingGamesPage = {
      resources: [game("played"), game("open")],
    } as never;
    store.finishedGamesPage = {
      resources: [game("read"), game("done")],
    } as never;

    const { recruiting, finished } = useLeftSidebarGames();

    expect(idsOf(recruiting.value)).toEqual(["open"]);
    expect(idsOf(finished.value)).toEqual(["done"]);
  });

  it("draws a game of Мои игры the viewer leads and curates with the led, not under ~", () => {
    signIn(UserRole.Mentor);
    useGamesStore().participatingGames = [
      game("curated", [GameParticipation.Moderator]),
      game("led", [GameParticipation.Owner, GameParticipation.Moderator]),
    ];

    const { own } = useLeftSidebarGames();

    expect(
      own.value.map((group) => [group.prefix, idsOf(group.games)]),
    ).toEqual([
      ["~ ", ["curated"]],
      [undefined, ["led"]],
    ]);
  });

  // A game in the first group it fits: the curated before the played and the read.
  it("draws a curated game of Мои игры once, under ~, whatever else the viewer is in it", () => {
    signIn();
    useGamesStore().participatingGames = [
      game("played", [GameParticipation.Moderator, GameParticipation.Player]),
      game("read", [GameParticipation.Moderator, GameParticipation.Reader]),
    ];

    const { own } = useLeftSidebarGames();

    expect(
      own.value.map((group) => [group.prefix, idsOf(group.games)]),
    ).toEqual([["~ ", ["played", "read"]]]);
  });

  it("leaves out of a guest's Активные and Завершенные игры what a block above shows", () => {
    const store = useGamesStore();
    store.recruitingGamesPage = { resources: [game("a")] } as never;
    store.activeGamesPage = { resources: [game("a"), game("b")] } as never;
    store.finishedGamesPage = {
      resources: [game("a"), game("b"), game("c")],
    } as never;

    const { active, finished } = useLeftSidebarGames();

    expect(idsOf(active.value)).toEqual(["b"]);
    expect(idsOf(finished.value)).toEqual(["c"]);
  });

  it("keeps Набор игроков at its length", () => {
    signIn();
    const store = useGamesStore();
    const mine = [game("m1", [GameParticipation.Player])];
    store.participatingGames = mine;
    const open = Array.from(
      { length: SIDEBAR_GAME_ROWS.recruiting + 1 },
      (_, at) => game(at === 0 ? "m1" : `o${at}`),
    );
    store.recruitingGamesPage = { resources: open } as never;

    const { recruiting } = useLeftSidebarGames();

    expect(recruiting.value).toHaveLength(SIDEBAR_GAME_ROWS.recruiting);
    expect(idsOf(recruiting.value)).not.toContain("m1");
  });
});

describe("the blogs of the left column", () => {
  it("leaves out of Активные блоги the blogs of Мои блоги", () => {
    signIn();
    const store = useBlogsStore();
    store.participatingBlogs = [blog("mine")];
    store.activeBlogs = [blog("mine"), blog("other")];

    const { active } = useLeftSidebarBlogs();

    expect(idsOf(active.value)).toEqual(["other"]);
  });

  it("leaves nothing out for a guest", () => {
    const store = useBlogsStore();
    store.participatingBlogs = [blog("mine")];
    store.activeBlogs = [blog("mine"), blog("other")];

    const { active } = useLeftSidebarBlogs();

    expect(idsOf(active.value)).toEqual(["mine", "other"]);
  });
});

describe("a block that came short", () => {
  const LIMIT = 3;

  function watchList(fetched: number, take: number, above: string[]) {
    const items = ref(
      Array.from({ length: fetched }, (_, at) => game(`g${at}`)),
    );
    const aboveIds = ref<ReadonlySet<string>>(new Set(above));
    const rows = () =>
      items.value.filter((row) => !aboveIds.value.has(row.id)).slice(0, LIMIT);
    const widen = vi.fn();
    const scope = effectScope();
    scope.run(() =>
      useFilledRows({
        items: () => items.value,
        rows,
        above: () => aboveIds.value,
        limit: LIMIT,
        take: () => take,
        widen,
      }),
    );
    return { widen, scope };
  }

  it("asks for as many more rows as the blocks above could take", async () => {
    const { widen, scope } = watchList(3, 3, ["g0", "g1", "x"]);
    await nextTick();

    expect(widen).toHaveBeenCalledWith(LIMIT + 3);
    scope.stop();
  });

  it("asks nothing when the server has no more than it sent", async () => {
    const { widen, scope } = watchList(2, 3, ["g0"]);
    await nextTick();

    expect(widen).not.toHaveBeenCalled();
    scope.stop();
  });

  it("asks nothing when nothing was left out", async () => {
    const { widen, scope } = watchList(3, 3, ["x"]);
    await nextTick();

    expect(widen).not.toHaveBeenCalled();
    scope.stop();
  });

  // The blocks above show as many games as the server gives at once, all of the page.
  const ALL_ABOVE = Array.from(
    { length: MAX_API_PAGE_SIZE },
    (_, at) => `g${at}`,
  );

  it("asks for no more rows than the server gives at once", async () => {
    const { widen, scope } = watchList(LIMIT, LIMIT, ALL_ABOVE);
    await nextTick();

    expect(widen).toHaveBeenCalledWith(MAX_API_PAGE_SIZE);
    scope.stop();
  });

  // A read that came back short at the most the server gives is all there is.
  it("asks nothing again at the rows it asked for", async () => {
    const most = MAX_API_PAGE_SIZE;
    const { widen, scope } = watchList(most, most, ALL_ABOVE);
    await nextTick();

    expect(widen).not.toHaveBeenCalled();
    scope.stop();
  });
});
