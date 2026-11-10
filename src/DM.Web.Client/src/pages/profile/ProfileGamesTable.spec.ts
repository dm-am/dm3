/**
 * @vitest-environment jsdom
 */

// The games of the profile's "Игры" tab: the "Ведущий" it opens on, the hint
// of the assistants beside the master, reached from the keyboard, and the
// status of the player's character under "Игрок".
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia } from "pinia";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { useAuthStore } from "@/shared/stores/auth";
import ProfileGamesTable from "./ProfileGamesTable.vue";

const { searchGames } = vi.hoisted(() => ({ searchGames: vi.fn() }));

vi.mock("@/entities/game/api/gameCatalogApi", () => ({
  default: { searchGames },
}));

const game = {
  id: "g-1",
  publicId: "g-1",
  title: "Игра",
  status: "Active",
  master: { username: "author" },
  assistants: [{ username: "helper" }],
  participation: [],
};

const answer = (rows: unknown[]) => ({
  data: {
    resources: rows,
    paging: { skip: 0, pages: 1, total: rows.length },
  },
  error: null,
});

let mounted: VueWrapper | null = null;
let address = () => "";

afterEach(() => {
  mounted?.unmount();
  mounted = null;
});

beforeEach(() => vi.clearAllMocks());

async function render(pageSize?: number, at = "/users/author") {
  const pinia = createPinia();
  // The reader's page size of lists, from the account settings.
  if (pageSize)
    useAuthStore(pinia).user = {
      username: "reader",
      settings: { pageSize },
    } as never;
  const page = await mountPage(ProfileGamesTable, at, {
    routes: [
      ["profile", "/users/:username"],
      ["game", "/game/:id"],
      ["game-reviews", "/game/:id/reviews"],
      ["game-first-unread-post", "/game/:id/posts/unread"],
      ["game-first-unread-comment", "/game/:id/comments/unread"],
    ],
    pinia,
    props: { username: "author" },
    stubs: {
      CounterPair: true,
      Paging: true,
      UserLink: true,
      GameStatusBadge: true,
    },
  });
  mounted = page.wrapper;
  address = () => page.router.currentRoute.value.fullPath;
  await flushPromises();
  return mounted;
}

const pressed = (wrapper: VueWrapper) =>
  wrapper.get('.role-toggle [aria-pressed="true"]').text();

describe("ProfileGamesTable", () => {
  it("stays on Ведущий when the user hosts nothing, and asks once", async () => {
    searchGames.mockResolvedValue(answer([]));

    const wrapper = await render();

    expect(pressed(wrapper)).toBe("Ведущий");
    expect(wrapper.text()).toContain("Пользователь не ведет игр");
    expect(searchGames).toHaveBeenCalledTimes(1);
    expect(searchGames.mock.calls[0][0]).toMatchObject({
      hostUsernames: ["author"],
    });
  });

  it("names the player's character as its card does, and an active one В игре", async () => {
    const character = (status: string, flags = {}) => ({
      name: "Герой",
      status,
      isDead: false,
      isPlayerLeft: false,
      isPlayerExiled: false,
      ...flags,
    });
    searchGames.mockResolvedValue(
      answer(
        [
          character("Active"),
          character("UnderReview"),
          character("Retired"),
          character("Retired", { isPlayerLeft: true, isPlayerExiled: true }),
        ].map((one, i) => ({ ...game, id: `g-${i}`, playerCharacters: [one] })),
      ),
    );
    const wrapper = await render();

    const player = wrapper
      .findAll(".role-toggle button")
      .find((segment) => segment.text() === "Игрок")!;
    await player.trigger("click");
    await flushPromises();

    expect(wrapper.findAll(".character-status").map((c) => c.text())).toEqual([
      "В игре",
      "На рассмотрении",
      "Вне игры",
      "Покинул игру",
    ]);
  });

  it("pages as many games as the reader asked for lists, and twenty without a choice", async () => {
    searchGames.mockResolvedValue(answer([]));

    await render(50);
    expect(searchGames.mock.calls[0][0]).toMatchObject({ size: 50 });

    mounted?.unmount();
    searchGames.mockClear();
    await render();
    expect(searchGames.mock.calls[0][0]).toMatchObject({ size: 20 });
  });

  it("asks for the page the address names", async () => {
    searchGames.mockResolvedValue(answer([]));

    await render(undefined, "/users/author?number=3");

    expect(searchGames.mock.calls[0][0]).toMatchObject({ number: 3 });
  });

  it("goes back to the first page of the address when the role changes", async () => {
    searchGames.mockResolvedValue(answer([]));
    const wrapper = await render(undefined, "/users/author?number=3");

    const player = wrapper
      .findAll(".role-toggle button")
      .find((segment) => segment.text() === "Игрок")!;
    await player.trigger("click");
    await flushPromises();

    expect(address()).toBe("/users/author");
  });

  it("lets the keyboard reach the hint of the assistants", async () => {
    searchGames.mockResolvedValue(answer([game]));

    const wrapper = await render();

    const count = wrapper.get(".assistant-count");
    expect(count.text()).toBe("[+1]");
    expect(
      count.element.closest(".tooltip-trigger")?.getAttribute("tabindex"),
    ).toBe("0");
  });
});
