/**
 * @vitest-environment jsdom
 */

// A list loading and a list that failed are not an empty list: the store sets
// an error and nulls the page (GameCharacters.spec.ts holds the same for the roster).
import { afterEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { messagingApi, useMessagingStore } from "@/entities/message";
import { useAuthStore, userApi } from "@/entities/user";
import ChatsList from "./ChatsList.vue";

enableAutoUnmount(afterEach);
afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

const push = vi.hoisted(() => vi.fn());

vi.mock("vue-router", () => ({
  useRoute: () => ({ query: {} }),
  useRouter: () => ({ push }),
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

  // The list stands whole at one width: no magnifier stands in for the field.
  it("always has the field of the search", () => {
    const { wrapper } = render();

    expect(wrapper.get("input").attributes("placeholder")).toBe(
      "Поиск собеседника",
    );
    expect(
      wrapper.find('button[aria-label="Поиск собеседника"]').exists(),
    ).toBe(false);
  });
});

// The field over the list: the reader's people and groups before a name, anyone by name after.
describe("ChatsList, the search of a person", () => {
  const ME = "Ланселот";
  const person = (username: string) => ({ username, picture: null });
  const chat = (id: string, over: Record<string, unknown>) => ({
    id,
    unreadMessagesCount: 0,
    ...over,
  });

  function renderSearch() {
    const conversations = vi.spyOn(messagingApi, "getChats").mockResolvedValue({
      data: {
        resources: [
          chat("d-1", {
            type: "Direct",
            participants: [person(ME), person("Мордред")],
          }),
          chat("g-1", {
            type: "Group",
            title: "Круглый стол",
            participants: [person(ME)],
          }),
          chat("d-2", {
            type: "Direct",
            participants: [person(ME), person("Артур")],
          }),
          chat("g-2", {
            type: "Group",
            title: null,
            participants: [person(ME)],
          }),
        ],
      },
      error: null,
    } as never);
    const { wrapper } = render();
    useAuthStore().user = { username: ME } as never;
    push.mockClear();
    return { wrapper, conversations };
  }

  const offered = (wrapper: ReturnType<typeof renderSearch>["wrapper"]) =>
    wrapper.findAll(".autocomplete-item").map((row) => row.text());

  it("offers the people and groups of the reader's conversations by name before a name is typed, and opens the one picked", async () => {
    const { wrapper, conversations } = renderSearch();

    const field = wrapper.get("input");
    await field.trigger("focusin");
    await field.trigger("focus");
    await flushPromises();

    expect(conversations).toHaveBeenCalledWith({ take: 200 });
    expect(offered(wrapper)).toEqual([
      "Артур",
      "Групповая переписка",
      "Круглый стол",
      "Мордред",
    ]);
    await wrapper.findAll(".autocomplete-item")[2].trigger("mousedown");

    expect(push).toHaveBeenCalledWith({
      name: "chat",
      params: { id: "g-1" },
      query: {},
    });
  });

  it("writes to a person found by name, and never offers the reader to themselves", async () => {
    vi.useFakeTimers();
    vi.spyOn(userApi, "searchUsers").mockResolvedValue({
      data: { resources: [person(ME), person("Моргана")] },
      error: null,
    } as never);
    const { wrapper } = renderSearch();

    const field = wrapper.get("input");
    await field.trigger("focus");
    await field.setValue("Мо");
    await vi.advanceTimersByTimeAsync(200);
    await flushPromises();

    expect(offered(wrapper)).toEqual(["Моргана"]);
    await wrapper.get(".autocomplete-item").trigger("mousedown");

    expect(push).toHaveBeenCalledWith({
      name: "direct-message",
      params: { username: "Моргана" },
      query: {},
    });
    expect((field.element as HTMLInputElement).value).toBe("");
  });
});
