/**
 * @vitest-environment jsdom
 */

/**
 * What the chat does when its socket comes back.
 *
 * The feed was always caught up on that transition; the events strip was not.
 * It is read once, when the page mounts, and afterwards only a push about an
 * event starting or ending moves it — which is precisely what a socket that is
 * down does not deliver. So a tab that lost the connection over an event and
 * regained it afterwards went on announcing an event that had ended, and the
 * page gave the reader no way to find out but a reload.
 *
 * The page's own spec next door parses the template and runs without a DOM;
 * this one mounts the page, so it lives in a file of its own.
 */
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
  // Filled by the mock factory below, which is where `ref` can be reached.
  socket: { isConnected: null as unknown as { value: boolean } },
  scrollToIndex: vi.fn(),
}));

vi.mock("@/entities/global-chat/api/globalChatApi", () => ({ default: api }));

/**
 * Stands in for the shared connection so the test can drive the one thing the
 * page watches. The real composable negotiates a hub connection on connect().
 */
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

/**
 * The virtualizer, replaced by the one thing asked of it here: which row the
 * page scrolls to. The real one measures elements, and jsdom lays nothing out.
 */
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

// The page opens a warning dialog through it at setup; mounting the real modal
// machinery would drag a teleport target in for a dialog nothing here opens.
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

/**
 * Moves the clock forward for everything that reads Date.now(). The socket
 * coming up leaves the events list alone when it has only just been read, and
 * on mount it always has been: without this every reconnect in this file would
 * be one that happened a moment after the page opened.
 */
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

  /**
   * The page reads the events on mount, and the first connection normally
   * follows a moment later: reading them again there asks the same question
   * twice on every open of the chat, and the two answers can land in either
   * order.
   */
  it("does not read the events twice when the socket comes up on open", async () => {
    await openChat();
    expect(api.getEvents).toHaveBeenCalledTimes(1);

    socket.isConnected.value = true;
    await flushPromises();

    expect(api.getEvents).toHaveBeenCalledTimes(1);
  });

  /**
   * And not when the connection lands while that read is still in flight,
   * which is the ordinary case on a slow answer. The moment of the read used
   * to be written down when the answer came back, so at this point nothing had
   * been recorded yet and the socket asked the same question a second time.
   */
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

  /**
   * Going down is not a transition worth a request: there is nothing to read
   * the list from, and the socket dropping cannot have changed it. The feed is
   * still caught up on it, for messages that landed before the drop.
   */
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

  /**
   * A reader at the tail is still at the tail once the window is reloaded, and
   * a window of fifty is replaced by another window of fifty: a full page is
   * what the server answers with whenever the chat holds fifty live messages.
   * So counting them says the feed has not moved at the one moment every line
   * on it has been replaced, and the reader is left wherever the old offset
   * happens to land, reading the middle of a conversation he has not seen.
   */
  it("keeps a reader at the tail there when the window is reloaded", async () => {
    api.getMessages.mockResolvedValue(messagePage(run(0)));
    await openChat();
    const store = useGlobalChatStore();
    scrollToIndex.mockClear();

    // The socket dropped, and by the time it was back the whole window had
    // aged out: fifty held against fifty strangers.
    api.getMessages.mockResolvedValue(messagePage(run(100)));
    socket.isConnected.value = true;
    await flushPromises();
    await nextTick();

    expect(store.messages).toHaveLength(50);
    expect(store.messages[49].id).toBe("m149");
    expect(scrollToIndex).toHaveBeenCalled();
  });

  /**
   * The reader who scrolled up decides the fate of the window, and the page is
   * the only one who can measure that: the store holds no scroll position.
   * Passing the measurement on is what stops a drift wider than a page from
   * replacing every line under a reader who is in the middle of it, so the call
   * that forgets to pass it leaves the store reading the reader as if he stood
   * at the tail.
   */
  it("tells the store where the reader stands", async () => {
    api.getMessages.mockResolvedValue(messagePage(run(0)));
    await openChat();
    const store = useGlobalChatStore();
    const poll = vi.spyOn(store, "pollForNewer");

    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    // A boolean, whatever its value: the call that forgets the argument leaves
    // the store on its default and reads like a reader standing at the tail,
    // which is the one case where replacing the window is safe.
    expect(poll).toHaveBeenCalledWith(expect.any(Boolean));
  });

  /**
   * The tab coming back to the screen is a re-read in every case, live socket
   * included: nothing replays what the hub sent while the tab was away, and a
   * background socket the browser throttled reports itself connected all the
   * same. The tick of the fallback timer steps aside for a live socket on
   * purpose, and it used to be what the return of the tab ran, so the one
   * moment the reader is looking at the page again was the one moment nothing
   * was checked.
   */
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
