/**
 * @vitest-environment jsdom
 */

// A #msg- link or an archive date that finds nothing shows the latest messages
// with a line above the feed that says so and names the day. The link of a
// message is an ordinary link, and a press on it raises nothing.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import type { Router } from "vue-router";
import { mountPage } from "@/shared/lib/testing/mountPage";

const { api, virtualRows } = vi.hoisted(() => ({
  api: {
    getMessages: vi.fn(),
    getEvents: vi.fn(),
    getEvent: vi.fn(),
    getMessagesAround: vi.fn(),
    getMessagesNearDate: vi.fn(),
    getMessagesAfter: vi.fn(),
  },
  // Filled by the mock factory below, where `ref` can be reached.
  virtualRows: { held: null as unknown as { value: unknown[] } },
}));

vi.mock("@/entities/global-chat/api/globalChatApi", () => ({ default: api }));

vi.mock("@/shared/lib/composables/useSignalR", async () => {
  const { ref } = await import("vue");
  return {
    useGlobalSignalR: () => ({
      connect: vi.fn(),
      disconnect: vi.fn(),
      onNotification: () => () => {},
      isConnected: ref(false),
    }),
  };
});

/** The virtualizer, reduced to the rows the test puts on screen. */
vi.mock("@/shared/lib/composables/useVirtualScroll", async () => {
  const { ref } = await import("vue");
  virtualRows.held = ref([]);
  return {
    useVirtualScroll: () => ({
      virtualizer: ref(null),
      virtualItems: virtualRows.held,
      totalSize: ref(0),
      measureElement: vi.fn(),
      scrollToIndex: vi.fn(),
      range: ref({ startIndex: 0, endIndex: 0 }),
    }),
  };
});

import GlobalChatPage from "./GlobalChatPage.vue";
import { ChatSearchRow } from "@/widgets/chat-feed";
import { useToast } from "@/shared/lib/composables/useToast";

enableAutoUnmount(afterEach);
afterEach(() => {
  vi.restoreAllMocks();
});

/** The tooltip, reduced to what it would say. */
const TooltipStub = defineComponent({
  props: { text: { type: String, default: "" } },
  setup(props, { slots }) {
    return () =>
      h("span", { "data-tooltip": props.text }, slots.default?.() ?? []);
  },
});

const message = (id: number) => ({
  id: `m${id}`,
  text: `text ${id}`,
  createdUtc: new Date(2026, 2, 14, 12, 0, id).toISOString(),
  author: null,
  likes: [],
});

const messagePage = (ids: number[]) => ({
  data: {
    resources: ids.map(message),
    paging: {
      prevCursor: null,
      nextCursor: null,
      hasPrev: false,
      hasNext: false,
    },
  },
  error: null,
});

const refused = () => ({
  data: null,
  error: { status: 500, title: "Сервер не ответил" },
});

let pinia: Pinia;
let router: Router;

/** Every child but the feed, its edges, notice and toolbar, as a shallow mount would leave them. */
const stubs = {
  PageTitle: true,
  ChatSearchRow: true,
  ChatEventsPanel: true,
  LoginPrompt: true,
  ChatComposer: true,
  ChatMessage: true,
  ChatMessageSkeleton: true,
  ErrorState: true,
  DashSeparator: true,
  ActionResult: true,
  SvgIcon: true,
  Tooltip: TooltipStub,
};

async function openChat(address = "/global-chat") {
  const mounted = await mountPage(GlobalChatPage, address, {
    routes: [["global-chat", "/global-chat"]],
    pinia,
    stubs,
  });
  router = mounted.router;
  const wrapper = mounted.wrapper;
  await flushPromises();
  await nextTick();
  return wrapper;
}

type Wrapper = Awaited<ReturnType<typeof openChat>>;

const notice = (wrapper: Wrapper) => wrapper.find(".chat-feed__notice");

/** Every plate on screen; the store behind useToast is one for the whole run. */
const plates = () => useToast().toasts.value;

async function settle() {
  await flushPromises();
  await nextTick();
}

beforeEach(() => {
  vi.clearAllMocks();
  // jsdom has neither, and the feed is virtualized behind both.
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
  vi.stubGlobal(
    "IntersectionObserver",
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
  pinia = createPinia();
  setActivePinia(pinia);
  api.getMessages.mockResolvedValue(messagePage([1, 2]));
  api.getEvents.mockResolvedValue({ data: { resources: [] }, error: null });
  virtualRows.held.value = [];
  history.replaceState(null, "", window.location.pathname);
  const toast = useToast();
  for (const plate of [...plates()]) toast.dismiss(plate.id);
});

describe("GlobalChatPage, a link to a message that is gone", () => {
  it("says so above the feed, in words, and raises no plate", async () => {
    window.location.hash = "#msg-gone";
    api.getMessagesAround.mockResolvedValue(messagePage([]));

    const wrapper = await openChat();

    expect(notice(wrapper).exists()).toBe(true);
    expect(notice(wrapper).text()).toBe(
      "Сообщение по ссылке не найдено или удалено, показаны последние сообщения",
    );
    expect(notice(wrapper).attributes("role")).toBe("status");
    // The tail did load under the line.
    expect(api.getMessages).toHaveBeenCalled();
    expect(plates()).toEqual([]);
  });

  it("says nothing when the linked message is there", async () => {
    window.location.hash = "#msg-m1";
    api.getMessagesAround.mockResolvedValue(messagePage([1, 2]));

    const wrapper = await openChat();

    expect(notice(wrapper).exists()).toBe(false);
  });

  // A failed request was announced by the interceptor; the page does not call it "not found".
  it("does not call a failed request a missing message", async () => {
    window.location.hash = "#msg-m1";
    api.getMessagesAround.mockResolvedValue(refused());

    const wrapper = await openChat();

    expect(notice(wrapper).exists()).toBe(false);
    expect(plates()).toEqual([]);
  });

  it("takes the line down when the reader asks for another place", async () => {
    window.location.hash = "#msg-gone";
    api.getMessagesAround.mockResolvedValue(messagePage([]));
    const wrapper = await openChat();
    expect(notice(wrapper).exists()).toBe(true);

    api.getMessagesAround.mockResolvedValue(messagePage([1, 2]));
    wrapper.findComponent(ChatSearchRow).vm.$emit("jump", "m1");
    await settle();

    expect(notice(wrapper).exists()).toBe(false);
  });
});

describe("GlobalChatPage, an archive date with nothing on it", () => {
  it("names the day above the feed, and raises no plate", async () => {
    api.getMessagesNearDate.mockResolvedValue(messagePage([]));

    const wrapper = await openChat("/global-chat?date=2019-03-05");

    expect(notice(wrapper).text()).toBe(
      "За 05.03.2019 сообщений нет, показаны последние сообщения",
    );
    // The URL has already lost the day: the line is the only place it is named.
    expect(router.currentRoute.value.fullPath).toBe("/global-chat");
    expect(plates()).toEqual([]);
  });

  // A day with nothing on it comes back as a window near it: the day stays
  // picked, with no line (useChatHistoryWindow.loadDate).
  it("keeps the day over the window near it, with no line", async () => {
    api.getMessagesNearDate.mockResolvedValue(messagePage([1, 2]));

    const wrapper = await openChat("/global-chat?date=2019-03-05");

    expect(notice(wrapper).exists()).toBe(false);
    expect(api.getMessages).not.toHaveBeenCalled();
    expect(router.currentRoute.value.query.date).toBe("2019-03-05");
  });

  it("takes the line down when a later day is there", async () => {
    api.getMessagesNearDate.mockResolvedValue(messagePage([]));
    const wrapper = await openChat("/global-chat?date=2019-03-05");
    expect(notice(wrapper).exists()).toBe(true);

    api.getMessagesNearDate.mockResolvedValue(messagePage([3, 4]));
    await router.push("/global-chat?date=2026-03-14");
    await settle();

    expect(notice(wrapper).exists()).toBe(false);
  });

  // The store keeps its window on a failure; the page does not call the day empty.
  it("does not call a failed request an empty day", async () => {
    const wrapper = await openChat();
    api.getMessagesNearDate.mockResolvedValue(refused());

    await router.push("/global-chat?date=2019-03-05");
    await settle();

    expect(notice(wrapper).exists()).toBe(false);
    expect(plates()).toEqual([]);
  });
});

// A picked day is drawn anew, so its sentinels are new elements, and the
// observers must watch them for the feed to page from that day.
describe("GlobalChatPage, paging from a picked day", () => {
  let observers: {
    callback: IntersectionObserverCallback;
    targets: Element[];
  }[] = [];

  beforeEach(() => {
    observers = [];
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        targets: Element[] = [];
        constructor(callback: IntersectionObserverCallback) {
          observers.push({ callback, targets: this.targets });
        }
        observe(target: Element) {
          this.targets.push(target);
        }
        unobserve() {}
        disconnect() {}
      },
    );
  });

  it("watches the bottom of the day's window and pages from there", async () => {
    vi.useFakeTimers({ toFake: ["setTimeout"] });
    const wrapper = await openChat();
    const day = messagePage([3, 4]);
    day.data.paging.hasNext = true;
    day.data.paging.nextCursor = "next-1" as never;
    api.getMessagesNearDate.mockResolvedValue(day);

    await router.push("/global-chat?date=2026-03-14");
    await settle();
    // The landing scroll settles before the sentinels wake.
    await vi.advanceTimersByTimeAsync(650);

    const sentinels = wrapper.findAll(".chat-feed__sentinel");
    const bottom = sentinels[sentinels.length - 1].element;
    const watching = observers.filter((o) => o.targets.includes(bottom));
    expect(watching.length).toBeGreaterThan(0);

    api.getMessagesAfter.mockResolvedValue(messagePage([5]));
    watching[watching.length - 1].callback(
      [{ isIntersecting: true } as IntersectionObserverEntry],
      {} as IntersectionObserver,
    );
    await settle();

    expect(api.getMessagesAfter).toHaveBeenCalledWith("next-1", 50);
    vi.useRealTimers();
  });
});

describe("GlobalChatPage, the link of a message", () => {
  beforeEach(() => {
    // Two messages on screen; their date separator sits at index 0.
    virtualRows.held.value = [
      { index: 1, start: 0, key: "m1", size: 80 },
      { index: 2, start: 80, key: "m2", size: 80 },
    ];
  });

  async function hover(wrapper: Wrapper, id: string) {
    await wrapper.find(`#msg-${id}`).trigger("mouseenter");
    await nextTick();
  }

  const anchor = (wrapper: Wrapper) =>
    wrapper.find('a[aria-label="Ссылка на сообщение"]');

  const tooltipOf = (wrapper: Wrapper) =>
    anchor(wrapper).element.parentElement?.getAttribute("data-tooltip");

  it("leads to the message on this page, keeping the address it stands at", async () => {
    const wrapper = await openChat();
    await hover(wrapper, "m1");

    expect(anchor(wrapper).attributes("href")).toBe("/global-chat#msg-m1");
    expect(tooltipOf(wrapper)).toBe("Ссылка на сообщение");
  });

  // Followed on this page, the link changes the anchor only; the feed lands on the message.
  it("lands the feed on the message when followed on the page", async () => {
    await openChat();
    api.getMessagesAround.mockResolvedValue(messagePage([1, 2]));

    await router.push("/global-chat#msg-m2");
    await settle();

    expect(api.getMessagesAround).toHaveBeenCalledTimes(1);
    expect(api.getMessagesAround.mock.calls[0][0]).toBe("m2");
  });
});
