/**
 * @vitest-environment jsdom
 */

/**
 * "Активные игры" is the guest's block: a guest's list asks the server for the
 * rows the blocks above take, a viewer gets neither the block nor that read.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { Api } from "@/shared/api";
import {
  GameParticipation,
  SIDEBAR_GAME_ROWS,
  useGamesStore,
  type GameRef,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import ActiveGames from "./ActiveGames.vue";

vi.mock("vue-router", async (actual) => ({
  ...(await actual<typeof import("vue-router")>()),
  useRoute: () => ({ fullPath: "/" }),
}));

const stubs = { SidebarEntityList: true };

const game = (id: string, participation: GameParticipation[] = []) =>
  ({
    id,
    title: `Игра ${id}`,
    status: "Active",
    participation,
  }) as unknown as GameRef;

/** A full page of the block whose first game a block above shows. */
function fullPageUnder(above: GameRef) {
  const store = useGamesStore();
  store.activeGamesPage = {
    resources: [above, ...["b", "c", "d", "e"].map((id) => game(id))],
  } as never;
  return store;
}

/** The rows each read of the games asked the server for. */
function takesAsked() {
  const get = vi
    .spyOn(Api, "get")
    .mockResolvedValue({ data: { resources: [] }, error: null } as never);
  return () =>
    get.mock.calls
      .filter(([url]) => url === "games")
      .map(([, params]) => (params as { take: number }).take);
}

beforeEach(() => {
  setActivePinia(createPinia());
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("ActiveGames", () => {
  it("asks a guest's server for one row more when Набор игроков takes one", async () => {
    const store = fullPageUnder(game("open"));
    store.recruitingGamesPage = { resources: [game("open")] } as never;
    const takes = takesAsked();

    mount(ActiveGames, { global: { stubs } });
    await flushPromises();

    expect(takes()).toEqual([SIDEBAR_GAME_ROWS.active + 1]);
  });

  it("asks nothing for a viewer, whom the block is not drawn for", async () => {
    useAuthStore().user = { username: "viewer" } as never;
    const mine = game("mine", [GameParticipation.Player]);
    fullPageUnder(mine).participatingGames = [mine];
    const takes = takesAsked();

    const wrapper = mount(ActiveGames, { global: { stubs } });
    await flushPromises();

    expect(wrapper.html()).toBe("<!--v-if-->");
    expect(takes()).toEqual([]);
  });
});
