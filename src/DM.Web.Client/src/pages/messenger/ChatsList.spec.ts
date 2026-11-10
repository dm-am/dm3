/**
 * @vitest-environment jsdom
 */

// A list loading and a list that failed are not an empty list: the store sets
// an error and nulls the page (GameCharacters.spec.ts holds the same for the roster).
import { afterEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { messagingApi, useMessagingStore } from "@/entities/message";
import { useAuthStore, userApi } from "@/entities/user";
import { mountPage } from "@/shared/lib/testing/mountPage";
import ChatsList from "./ChatsList.vue";

enableAutoUnmount(afterEach);
afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

const EMPTY = "Нет переписок";

async function render(
  state: { loading?: boolean; error?: string | null } = {},
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useMessagingStore();
  // The endpoint is not the subject; the four states the page draws are.
  const fetchChats = vi.spyOn(store, "fetchChats").mockResolvedValue(undefined);
  store.chats = null;
  store.loadingChats = state.loading ?? false;
  store.error = state.error ?? null;

  const { wrapper, router } = await mountPage(ChatsList, "/messenger", {
    routes: [
      ["messenger", "/messenger"],
      ["chat", "/messenger/c/:id"],
      ["direct-message", "/messenger/user/:username"],
    ],
    pinia,
    attachTo: document.body,
  });

  return { wrapper, router, store, fetchChats };
}

describe("ChatsList", () => {
  it("says nothing about the conversations while they are being fetched", async () => {
    const { wrapper } = await render({ loading: true });

    expect(wrapper.text()).not.toContain(EMPTY);
    expect(wrapper.find(".chat-skeleton-list").exists()).toBe(true);
  });

  it("names a failed load instead of reporting no conversations", async () => {
    const { wrapper } = await render({
      error: "Не удалось загрузить переписки",
    });

    expect(wrapper.text()).not.toContain(EMPTY);
    expect(wrapper.text()).toContain("Не удалось загрузить переписки");
  });

  it("repeats the request without a page reload", async () => {
    const { wrapper, fetchChats } = await render({
      error: "Не удалось загрузить переписки",
    });
    fetchChats.mockClear();

    await wrapper.find(".error-retry").trigger("click");
    await flushPromises();

    expect(fetchChats).toHaveBeenCalled();
  });

  it("still says the truth when there is genuinely nothing", async () => {
    const { wrapper } = await render();

    expect(wrapper.text()).toContain(EMPTY);
  });

  // The list stands whole at one width: no magnifier stands in for the field.
  it("always has the field of the search", async () => {
    const { wrapper } = await render();

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

  async function renderSearch() {
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
    const { wrapper, router } = await render();
    useAuthStore().user = { username: ME } as never;
    return { wrapper, router, conversations };
  }

  const offered = (
    wrapper: Awaited<ReturnType<typeof renderSearch>>["wrapper"],
  ) => wrapper.findAll(".autocomplete-item").map((row) => row.text());

  it("offers the people and groups of the reader's conversations by name before a name is typed, and opens the one picked", async () => {
    const { wrapper, router, conversations } = await renderSearch();

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
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe("/messenger/c/g-1");
  });

  it("writes to a person found by name, and never offers the reader to themselves", async () => {
    vi.useFakeTimers();
    vi.spyOn(userApi, "searchUsers").mockResolvedValue({
      data: { resources: [person(ME), person("Моргана")] },
      error: null,
    } as never);
    const { wrapper, router } = await renderSearch();

    const field = wrapper.get("input");
    await field.trigger("focus");
    await field.setValue("Мо");
    await vi.advanceTimersByTimeAsync(200);
    await flushPromises();

    expect(offered(wrapper)).toEqual(["Моргана"]);
    await wrapper.get(".autocomplete-item").trigger("mousedown");
    await flushPromises();

    // The whole address: no query of the list rides along into the conversation.
    expect(router.currentRoute.value.name).toBe("direct-message");
    expect(router.currentRoute.value.fullPath).toBe(
      `/messenger/user/${encodeURIComponent("Моргана")}`,
    );
    expect((field.element as HTMLInputElement).value).toBe("");
  });
});
