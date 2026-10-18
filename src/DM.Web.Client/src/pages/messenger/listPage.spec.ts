/**
 * @vitest-environment jsdom
 */

/**
 * The list of conversations stands beside every correspondence and keeps its
 * page in the address, ?number, the key of the site's paging. A conversation
 * opened from the second page took the page out of the address, and the list
 * went back to its first page under the reader, the row just pressed going
 * with it. The page of the list is kept by every move inside the messenger:
 * the row of a conversation here, the link of a message and the day of the
 * calendar in ChatView.spec.ts.
 */
import { afterEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { createMemoryHistory, createRouter, RouterView } from "vue-router";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useMessagingStore } from "@/entities/message";
import ChatsList from "./ChatsList.vue";
import { listPageQuery } from "./listPage";

vi.mock("@/shared/lib/composables/useSignalR", () => ({
  useGlobalSignalR: () => ({ onNotification: () => () => {} }),
}));

enableAutoUnmount(afterEach);

/** The messenger reduced to its two halves: the list and the outlet. */
const Messenger = defineComponent(
  () => () => h("div", [h(ChatsList), h(RouterView)]),
);
const Blank = defineComponent(() => () => h("div"));

async function openAt(address: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: "/messenger",
        component: Messenger,
        children: [
          { name: "messenger", path: "", component: Blank },
          { name: "chat", path: "c/:id", component: Blank },
        ],
      },
    ],
  });
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useMessagingStore();
  // The rows of whichever page is asked; the request is not the subject.
  const fetchChats = vi
    .spyOn(store, "fetchChats")
    .mockImplementation(async (page = 1) => {
      store.chats = {
        resources: [0, 1].map((i) => ({
          id: `p${page}c${i}`,
          type: "Direct",
          participants: [],
          unreadMessagesCount: 0,
          lastMessage: null,
        })),
        paging: null,
      } as never;
    });
  await router.push(address);
  await router.isReady();
  const wrapper = mount(
    defineComponent(() => () => h(RouterView)),
    { global: { plugins: [router, pinia] } },
  );
  await flushPromises();
  return { wrapper, router, fetchChats };
}

const pagesAsked = (fetchChats: { mock: { calls: unknown[][] } }) =>
  fetchChats.mock.calls.map((call) => call[0]);

describe("the page of the list of conversations", () => {
  it("stays when a conversation is opened from it", async () => {
    const { wrapper, router, fetchChats } = await openAt("/messenger?number=2");
    expect(pagesAsked(fetchChats)).toEqual([2]);

    await wrapper.findAll("a.chat-preview")[1].trigger("click");
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe(
      "/messenger/c/p2c1?number=2",
    );
    // The list was not read again, and so not at its first page.
    expect(pagesAsked(fetchChats)).toEqual([2]);
    expect(wrapper.find("a.chat-preview.router-link-active").text()).toBe(
      wrapper.findAll("a.chat-preview")[1].text(),
    );
  });

  it("is the first one where the address names none", async () => {
    const { wrapper, router, fetchChats } = await openAt("/messenger");

    await wrapper.findAll("a.chat-preview")[0].trigger("click");
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe("/messenger/c/p1c0");
    expect(pagesAsked(fetchChats)).toEqual([1]);
  });
});

describe("listPageQuery", () => {
  it("takes the page of the list and nothing else of the address", () => {
    expect(
      listPageQuery({ number: "3", msg: "m1", date: "2026-01-01" }),
    ).toEqual({ number: "3" });
  });

  it("is empty where the address has no page", () => {
    expect(listPageQuery({ msg: "m1" })).toEqual({});
    expect(listPageQuery({ number: "" })).toEqual({});
    expect(listPageQuery({ number: ["2", "3"] })).toEqual({});
  });
});
