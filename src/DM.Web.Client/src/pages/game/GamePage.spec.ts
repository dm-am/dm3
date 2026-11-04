/**
 * @vitest-environment jsdom
 */

// A failed load of the game is a page of its own status.
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
  // Not "удалена": the game endpoint answers Gone for a typo too.
  it("answers Gone with the page of an address that leads nowhere", () => {
    expect(render(410).find(".error-page").text()).toBe("404");
  });

  it("reads a request that never left as a server failure", () => {
    expect(render(0).find(".error-page").text()).toBe("500");
  });

  it("draws the title skeleton while the game is on the wire", () => {
    const wrapper = render(null);

    expect(wrapper.find(".error-page").exists()).toBe(false);
    expect(wrapper.find(".skeleton-title").exists()).toBe(true);
  });
});
