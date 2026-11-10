/**
 * @vitest-environment jsdom
 */

/**
 * "Активные игры" is the guest's block: a guest's list asks the server for the
 * rows the blocks above take, a viewer gets neither the block nor that read.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { Api } from "@/shared/api";
import {
  GameParticipation,
  SIDEBAR_GAME_ROWS,
  useGamesStore,
  type GameRef,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import ActiveGames from "./ActiveGames.vue";

const stubs = { SidebarEntityList: true };
const routes: Address[] = [
  ["home", "/"],
  ["forum-index", "/forum"],
  ["games", "/games"],
];

let pinia: Pinia;
const render = () => mountPage(ActiveGames, "/", { routes, pinia, stubs });

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
  pinia = createPinia();
  setActivePinia(pinia);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("ActiveGames", () => {
  it("asks a guest's server for one row more when Набор игроков takes one", async () => {
    const store = fullPageUnder(game("open"));
    store.recruitingGamesPage = { resources: [game("open")] } as never;
    const takes = takesAsked();

    await render();
    await flushPromises();

    expect(takes()).toEqual([SIDEBAR_GAME_ROWS.active + 1]);
  });

  it("asks nothing for a viewer, whom the block is not drawn for", async () => {
    useAuthStore().user = { username: "viewer" } as never;
    const mine = game("mine", [GameParticipation.Player]);
    fullPageUnder(mine).participatingGames = [mine];
    const takes = takesAsked();

    const { wrapper } = await render();
    await flushPromises();

    expect(wrapper.findComponent(ActiveGames).html()).toBe("<!--v-if-->");
    expect(takes()).toEqual([]);
  });

  it("asks a guest's server again on a navigation after a failed read", async () => {
    const get = vi
      .spyOn(Api, "get")
      .mockResolvedValue({ data: null, error: { title: "Сбой" } } as never);
    const asked = () => get.mock.calls.filter(([url]) => url === "games");

    const { router } = await render();
    await flushPromises();
    expect(asked()).toHaveLength(1);

    await router.push("/forum");
    await flushPromises();

    expect(asked()).toHaveLength(2);
  });
});
