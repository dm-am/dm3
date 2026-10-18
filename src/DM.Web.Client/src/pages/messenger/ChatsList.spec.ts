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
import { defineComponent, h, ref, type Component } from "vue";
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

function render(
  state: { loading?: boolean; error?: string | null } = {},
  props: { narrow?: boolean } = {},
  component: Component = ChatsList,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useMessagingStore();
  // The endpoint is not the subject; the four states the page draws are.
  const fetchChats = vi.spyOn(store, "fetchChats").mockResolvedValue(undefined);
  store.chats = null;
  store.loadingChats = state.loading ?? false;
  store.error = state.error ?? null;

  const wrapper = mount(component, {
    props,
    attachTo: document.body,
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

  /**
   * Narrow, the list keeps the pictures and the counts; its search is a
   * magnifier that asks for the words back, and the caret goes in the field.
   */
  it("searches from its magnifier when narrow, and asks to be widened", async () => {
    // The page that holds the list, as MessengerPage does: narrow until the
    // list asks for its words.
    const narrow = ref(true);
    const widen = vi.fn(() => {
      narrow.value = false;
    });
    const Host = defineComponent(
      () => () => h(ChatsList, { narrow: narrow.value, onWiden: widen }),
    );
    const { wrapper } = render({}, {}, Host);

    expect(wrapper.find("input").exists()).toBe(false);
    const magnifier = wrapper.get('button[aria-label="Поиск собеседника"]');
    await magnifier.trigger("click");
    await flushPromises();

    expect(widen).toHaveBeenCalledTimes(1);
    expect(document.activeElement).toBe(wrapper.get("input").element);
    wrapper.unmount();
  });

  it("has the field of the search when it has its words", () => {
    const { wrapper } = render();

    expect(wrapper.get("input").attributes("placeholder")).toBe(
      "Поиск собеседника",
    );
    expect(wrapper.find(".search-open").exists()).toBe(false);
    wrapper.unmount();
  });
});
