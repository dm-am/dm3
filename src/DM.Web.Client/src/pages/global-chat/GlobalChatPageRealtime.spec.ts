/**
 * @vitest-environment jsdom
 */

// What the chat does when its socket comes back: the feed is caught up, and so
// is the events strip, which only pushes move and a down socket does not deliver.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";

const { api, socket, scrollToIndex } = vi.hoisted(() => ({
  api: {
    getMessages: vi.fn(),
    getEvents: vi.fn(),
    getEvent: vi.fn(),
    getMessagesAround: vi.fn(),
    getMessagesNearDate: vi.fn(),
  },
  // Filled by the mock factory below, where `ref` can be reached.
  socket: { isConnected: null as unknown as { value: boolean } },
  scrollToIndex: vi.fn(),
}));

vi.mock("@/entities/global-chat/api/globalChatApi", () => ({ default: api }));

/** The shared connection, driven by the test; the real one negotiates a hub on connect(). */
vi.mock("@/shared/lib/composables/useSignalR", async () => {
  const { ref } = await import("vue");
  socket.isConnected = ref(false);
  return {
    useGlobalSignalR: () => ({
      connect: vi.fn(),
      disconnect: vi.fn(),
      onNotification: () => () => {},
      isConnected: socket.isConnected,
    }),
  };
});

/** The virtualizer, reduced to the row the page scrolls to; jsdom lays nothing out. */
vi.mock("@/shared/lib/composables/useVirtualScroll", async () => {
  const { ref } = await import("vue");
  return {
    useVirtualScroll: () => ({
      virtualizer: ref(null),
      virtualItems: ref([]),
      totalSize: ref(0),
      measureElement: vi.fn(),
      scrollToIndex,
      range: ref({ startIndex: 0, endIndex: 0 }),
    }),
  };
});

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

// The page opens a warning dialog through it; nothing here opens one.
vi.mock("vue-final-modal", () => ({
  useModal: () => ({ open: vi.fn(), close: vi.fn() }),
}));

import GlobalChatPage from "./GlobalChatPage.vue";
import { useGlobalChatStore } from "@/entities/global-chat";

enableAutoUnmount(afterEach);
afterEach(() => {
  vi.restoreAllMocks();
});

const message = (id: number) => ({
  id: `m${id}`,
  text: `text ${id}`,
  createdUtc: new Date(2026, 2, 14, 12, 0, id).toISOString(),
  author: null,
  likes: [],
});

/** A full latest page, the size the server caps `limit: 50` at. */
const run = (from: number) => Array.from({ length: 50 }, (_, i) => from + i);

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

let pinia: Pinia;

/** Moves Date.now() forward: the socket leaves a list read a moment ago alone, and mount has just read it. */
function pretendTimePassed(ms: number) {
  const now = Date.now();
  vi.spyOn(Date, "now").mockReturnValue(now + ms);
}

async function openChat() {
  const wrapper = mount(GlobalChatPage, {
    shallow: true,
    global: { plugins: [pinia], stubs: { ChatFeed: false } },
  });
  await flushPromises();
  return wrapper;
}

describe("GlobalChatPage, the socket coming back", () => {
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
    socket.isConnected.value = false;
  });

  it("re-reads the feed and the events strip", async () => {
    await openChat();
    api.getMessages.mockClear();
    api.getEvents.mockClear();
    // The connection came back after a real outage, not a moment after mount.
    pretendTimePassed(60_000);

    socket.isConnected.value = true;
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalled();
    expect(api.getEvents).toHaveBeenCalled();
  });

  // The first connection lands a moment after mount, which has read the events already.
  it("does not read the events twice when the socket comes up on open", async () => {
    await openChat();
    expect(api.getEvents).toHaveBeenCalledTimes(1);

    socket.isConnected.value = true;
    await flushPromises();

    expect(api.getEvents).toHaveBeenCalledTimes(1);
  });

  // Nor when the connection lands while that read is in flight.
  it("does not read the events twice when the socket comes up during the read", async () => {
    let answer!: (value: unknown) => void;
    api.getEvents.mockReturnValueOnce(
      new Promise((resolve) => {
        answer = resolve;
      }),
    );

    mount(GlobalChatPage, {
      shallow: true,
      global: { plugins: [pinia], stubs: { ChatFeed: false } },
    });
    socket.isConnected.value = true;
    await flushPromises();
    answer({ data: { resources: [] }, error: null });
    await flushPromises();

    expect(api.getEvents).toHaveBeenCalledTimes(1);
  });

  // Going down changes nothing to read; the feed is still caught up on it.
  it("asks for the events only on the way up", async () => {
    socket.isConnected.value = true;
    await openChat();
    api.getMessages.mockClear();
    api.getEvents.mockClear();

    socket.isConnected.value = false;
    await flushPromises();

    expect(api.getEvents).not.toHaveBeenCalled();
    expect(api.getMessages).toHaveBeenCalled();
  });

  // A window of fifty replaced by fifty: the last message, not the count, says the feed moved.
  it("keeps a reader at the tail there when the window is reloaded", async () => {
    api.getMessages.mockResolvedValue(messagePage(run(0)));
    await openChat();
    const store = useGlobalChatStore();
    scrollToIndex.mockClear();

    // The window aged out while the socket was down: fifty held against fifty strangers.
    api.getMessages.mockResolvedValue(messagePage(run(100)));
    socket.isConnected.value = true;
    await flushPromises();
    await nextTick();

    expect(store.messages).toHaveLength(50);
    expect(store.messages[49].id).toBe("m149");
    expect(scrollToIndex).toHaveBeenCalled();
  });

  // The store holds no scroll position, so the page passes where the reader stands.
  it("tells the store where the reader stands", async () => {
    api.getMessages.mockResolvedValue(messagePage(run(0)));
    await openChat();
    const store = useGlobalChatStore();
    const poll = vi.spyOn(store, "pollForNewer");

    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    // A boolean: a call that forgets it reads like a reader at the tail.
    expect(poll).toHaveBeenCalledWith(expect.any(Boolean));
  });

  // The tab coming back re-reads in every case, a live socket included: the hub
  // replays nothing and a throttled socket still reports itself connected.
  it("re-reads the feed and the events when the tab comes back", async () => {
    socket.isConnected.value = true;
    await openChat();
    api.getMessages.mockClear();
    api.getEvents.mockClear();

    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalled();
    expect(api.getEvents).toHaveBeenCalled();
  });
});
