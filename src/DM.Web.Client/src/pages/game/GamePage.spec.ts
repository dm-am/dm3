/**
 * @vitest-environment jsdom
 */

// A failed load of the game is a page of its own status.
import { describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { createMockGame } from "@/entities/game/model/store.fixtures";
import { mountPage } from "@/shared/lib/testing/mountPage";
import GamePage from "./GamePage.vue";

// The shell fetches on mount; what it draws afterwards is the subject.
vi.mock("@/shared/lib/composables/useFetchData", () => ({
  useFetchData: () => {},
}));

async function render(status: number | null, title?: string) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = title ? createMockGame("the-game-guid", title) : null;
  store.gameErrorStatus = status;
  store.gameError = status === null ? null : "Не удалось загрузить игру";

  const { wrapper } = await mountPage(GamePage, "/game/the-game/comments", {
    routes: [
      [
        "game-comments",
        "/game/:id/comments",
        { meta: { section: "Обсуждение" } },
      ],
    ],
    pinia,
    stubs: {
      RouterView: true,
      ErrorPage: {
        props: ["code"],
        template: "<div class='error-page'>{{ code }}</div>",
      },
    },
  });
  return wrapper;
}

describe("GamePage failure", () => {
  // Not "удалена": the game endpoint answers Gone for a typo too.
  it("answers Gone with the page of an address that leads nowhere", async () => {
    expect((await render(410)).find(".error-page").text()).toBe("404");
  });

  it("reads a request that never left as a server failure", async () => {
    expect((await render(0)).find(".error-page").text()).toBe("500");
  });

  it("draws the title skeleton while the game is on the wire", async () => {
    const wrapper = await render(null);

    expect(wrapper.find(".error-page").exists()).toBe(false);
    expect(wrapper.find(".skeleton-title").exists()).toBe(true);
  });

  it("heads a loaded game with the section its address names", async () => {
    const wrapper = await render(null, "Игра");

    expect(wrapper.find(".game-header").text()).toContain("Обсуждение");
    expect(wrapper.find(".game-header").text()).toContain("Игра");
  });
});
