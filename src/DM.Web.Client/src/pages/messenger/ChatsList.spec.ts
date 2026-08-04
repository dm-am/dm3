/**
 * @vitest-environment jsdom
 */

/**
 * A list still loading and a list that failed to load are not an empty list,
 * and the messenger said "Нет переписок" — with a hint to go find someone — to
 * all three. The store had the facts the whole time: it sets an error and nulls
 * the page. The forum's comment list carries the rule in a comment of its own:
 * a failed load must not be presented as fake emptiness.
 *
 * One gate for the class. GameCharacters.spec.ts holds the same two cases for
 * the roster, the other page of this slice that answered the same way.
 */
import { describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useMessagingStore } from "@/entities/message";
import ChatsList from "./ChatsList.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ query: {} }),
  useRouter: () => ({ push: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

const EMPTY = "Нет переписок";

function render(state: { loading?: boolean; error?: string | null } = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useMessagingStore();
  // The endpoint is not the subject; the four states the page draws are.
  const fetchChats = vi.spyOn(store, "fetchChats").mockResolvedValue(undefined);
  store.chats = null;
  store.loadingChats = state.loading ?? false;
  store.error = state.error ?? null;

  const wrapper = mount(ChatsList, {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: { template: "<a><slot /></a>" } },
    },
  });

  return { wrapper, store, fetchChats };
}

describe("ChatsList", () => {
  it("says nothing about the conversations while they are being fetched", () => {
    const { wrapper } = render({ loading: true });

    expect(wrapper.text()).not.toContain(EMPTY);
    expect(wrapper.find(".chat-skeleton-list").exists()).toBe(true);
  });

  it("names a failed load instead of reporting no conversations", () => {
    const { wrapper } = render({ error: "Не удалось загрузить переписки" });

    expect(wrapper.text()).not.toContain(EMPTY);
    expect(wrapper.text()).toContain("Не удалось загрузить переписки");
  });

  it("repeats the request without a page reload", async () => {
    const { wrapper, fetchChats } = render({
      error: "Не удалось загрузить переписки",
    });
    fetchChats.mockClear();

    await wrapper.find(".error-retry").trigger("click");
    await flushPromises();

    expect(fetchChats).toHaveBeenCalled();
  });

  it("still says the truth when there is genuinely nothing", () => {
    const { wrapper } = render();

    expect(wrapper.text()).toContain(EMPTY);
  });
});
