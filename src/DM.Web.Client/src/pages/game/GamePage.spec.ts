/**
 * @vitest-environment jsdom
 */

/**
 * A game that was deleted, a game the viewer may not open and a server that
 * fell over are three different things to say. The shell said one sentence to
 * all three — "Не удалось загрузить игру", which reads as "try again" — because
 * the store kept the sentence and threw the status away. The forum has told the
 * four cases apart for its topics all along.
 */
import { describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import GamePage from "./GamePage.vue";

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({ params: { id: "the-game" }, meta: {} }),
}));

// The shell fetches on mount; what it draws afterwards is the subject.
vi.mock("@/shared/lib/composables/useFetchData", () => ({
  useFetchData: () => {},
}));

function render(status: number | null) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = null;
  store.gameErrorStatus = status;
  store.gameError = status === null ? null : "Не удалось загрузить игру";

  return mount(GamePage, {
    global: {
      plugins: [pinia],
      stubs: {
        RouterView: true,
        RouterLink: { template: "<a><slot /></a>" },
        ErrorPage: {
          props: ["code"],
          template: "<div class='error-page'>{{ code }}</div>",
        },
      },
    },
  });
}

describe("GamePage failure", () => {
  it.each([
    // Gone, not Removed: the endpoint answers Gone for a deleted game, for a
    // mistyped id and for a game hidden from this reader alike, so the page
    // that says "удалена" would be inventing a fact for two of the three.
    [410, "404"],
    [403, "403"],
    [404, "404"],
    [500, "500"],
  ])("answers %i with its own error page", (status, code) => {
    expect(render(status).find(".error-page").text()).toBe(code);
  });

  it("reads a request that never left as a server failure", () => {
    // client.ts fills status 0 for a request that did not reach the API.
    expect(render(0).find(".error-page").text()).toBe("500");
  });

  it("draws the title skeleton while the game is on the wire", () => {
    const wrapper = render(null);

    expect(wrapper.find(".error-page").exists()).toBe(false);
    expect(wrapper.find(".skeleton-title").exists()).toBe(true);
  });
});
