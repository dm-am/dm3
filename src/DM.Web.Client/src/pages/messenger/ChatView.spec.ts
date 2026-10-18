/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import { useMessagingStore } from "@/entities/message";
import { NotificationType } from "@/shared/api/models/notifications";

const { api, clearSpy } = vi.hoisted(() => ({
  api: {
    getChat: vi.fn(),
    getMessages: vi.fn(),
    markAsRead: vi.fn(),
    sendMessage: vi.fn(),
    getMessagesBefore: vi.fn(),
    getMessagesAfter: vi.fn(),
    getMessageForEdit: vi.fn(),
    clearHistory: vi.fn(),
    leaveChat: vi.fn(),
  },
  clearSpy: vi.fn(),
}));

vi.mock("@/entities/message/api/messagingApi", () => ({ default: api }));

const search = vi.hoisted(() => ({ searchMessages: vi.fn() }));

vi.mock("@/features/message-search/api/messageSearchApi", () => ({
  default: search,
}));

/**
 * The shared socket, reduced to what the page uses of it: the set of handlers
 * a push is delivered to, and the connected flag it watches. The real one is a
 * module-level singleton around a hub connection — nothing a test can push a
 * notification into.
 */
const hub = vi.hoisted(() => ({
  handlers: new Set<(notification: unknown) => void>(),
  connected: { value: false } as { value: boolean },
}));

vi.mock("@/shared/lib/composables/useSignalR", async () => {
  const { ref } = await import("vue");
  const connected = ref(false);
  hub.connected = connected;
  return {
    useGlobalSignalR: () => ({
      connect: vi.fn(),
      disconnect: vi.fn(),
      onNotification: (handler: (notification: unknown) => void) => {
        hub.handlers.add(handler);
        return () => hub.handlers.delete(handler);
      },
      isConnected: connected,
    }),
  };
});

/**
 * Stands in for the editor: a plain textarea bound to the same v-model, plus
 * the clear() the page calls. Mounting the real one would drag tiptap in, and
 * the assertions here are about which of the two the page calls and when.
 */
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: defineComponent({
    name: "BBCodeEditor",
    props: {
      modelValue: { type: String, default: "" },
      context: { type: String, default: undefined },
    },
    emits: ["update:modelValue", "submit"],
    setup(props, { emit, expose }) {
      expose({ clear: clearSpy, focus: vi.fn(), clearDraft: vi.fn() });
      return () =>
        h("textarea", {
          class: "editor-stub",
          value: props.modelValue,
          onInput: (e: Event) =>
            emit("update:modelValue", (e.target as HTMLTextAreaElement).value),
        });
    },
  }),
}));

/**
 * The toolbar the page is given, kept for the test to read. The real one: the
 * spy only remembers the last instance, so a test can put the toolbar over a
 * message without the rows, which jsdom does not lay out and the virtual list
 * therefore never draws.
 */
const toolbar = vi.hoisted(() => ({
  last: null as null | {
    hoveredMessageId: { value: string | null };
  },
}));

vi.mock("@/shared/lib/composables/useMessageToolbar", async (original) => {
  const real =
    await original<
      typeof import("@/shared/lib/composables/useMessageToolbar")
    >();
  return {
    useMessageToolbar: (...args: Parameters<typeof real.useMessageToolbar>) => {
      const made = real.useMessageToolbar(...args);
      toolbar.last = made;
      return made;
    },
  };
});

/**
 * The virtualizer the feed scrolls with, as it is, with its scroll to an item
 * remembered: jsdom lays nothing out, so "the feed was taken down to the last
 * message" is the call the feed makes, not a position it reaches.
 */
const virtual = vi.hoisted(() => ({ scrollToIndex: vi.fn() }));

vi.mock("@/shared/lib/composables/useVirtualScroll", async (original) => {
  const real =
    await original<
      typeof import("@/shared/lib/composables/useVirtualScroll")
    >();
  return {
    useVirtualScroll: (...args: Parameters<typeof real.useVirtualScroll>) => {
      const made = real.useVirtualScroll(...args);
      return {
        ...made,
        scrollToIndex: (
          ...call: Parameters<typeof made.scrollToIndex>
        ): void => {
          virtual.scrollToIndex(...call);
          made.scrollToIndex(...call);
        },
      };
    },
  };
});

/**
 * A route the test can move. The page and its history window watch the
 * address, and move it themselves (a picked day, the archive left), so the
 * router here does what the real one does: the query changes, and the
 * watchers see it change.
 */
const nav = vi.hoisted(() => ({
  route: null as unknown as {
    params: Record<string, string>;
    query: Record<string, string>;
  },
  push: vi.fn(),
  replace: vi.fn(),
}));

vi.mock("vue-router", async () => {
  const { reactive } = await import("vue");
  nav.route = reactive({ params: { id: "c1" }, query: {} });
  return {
    useRoute: () => nav.route,
    useRouter: () => ({
      push: nav.push,
      replace: nav.replace,
      resolve: () => ({ href: "/messenger/c/c1" }),
    }),
  };
});

const follow = (to: { query?: Record<string, string> }) => {
  nav.route.query = to.query ?? {};
};

import ChatView from "./ChatView.vue";
import { ChatSearchRow } from "@/widgets/chat-feed";
import { fromSiteInput } from "@/shared/lib/utils/datetime";

/**
 * enableAutoUnmount is deliberately not used: one test closes the
 * correspondence itself, to see what the page lets go of on the way out, and a
 * second unmount of the same app only makes Vue warn about unmounting what is
 * not mounted. The page a test opened is closed here instead, and the one that
 * closed itself is off the list by then.
 */
let closePage: (() => void) | null = null;

afterEach(() => {
  closePage?.();
});

const problem = (status: number, title = "") => ({
  type: "",
  title,
  status,
  traceId: "t",
});

const field = (wrapper: ReturnType<typeof mount>) =>
  wrapper.find(".editor-stub").element as HTMLTextAreaElement;

let pinia: Pinia;

/**
 * A router link that keeps its words, so a test can read the line it stands
 * in, and says where it leads in an attribute.
 */
const RouterLinkStub = defineComponent({
  name: "RouterLink",
  props: { to: { type: [String, Object], required: true } },
  setup(props, { slots }) {
    return () =>
      h("a", { "data-to": JSON.stringify(props.to) }, slots.default?.());
  },
});

/**
 * Opens the correspondence. `attached` puts it in the document, where the
 * focus can go: a test that asks where the caret went needs it there.
 */
async function openChat(options: { attached?: boolean } = {}) {
  const wrapper = mount(ChatView, {
    ...(options.attached ? { attachTo: document.body } : {}),
    global: {
      plugins: [pinia],
      stubs: { Teleport: true, RouterLink: RouterLinkStub },
    },
  });
  closePage = () => {
    closePage = null;
    wrapper.unmount();
  };
  await flushPromises();
  return wrapper;
}

/** Closes the open correspondence; the afterEach above then has nothing left. */
function closeChat() {
  closePage?.();
}

/** An answer held open, so a test can act while the request is in flight. */
function heldAnswer<T>() {
  let settle!: (answer: T) => void;
  const answer = new Promise<T>((resolve) => {
    settle = resolve;
  });
  return { answer, settle };
}

/**
 * document.visibilityState is a getter on the prototype and jsdom offers no
 * way to set it, so the property is shadowed on the document itself.
 */
function setVisibility(state: "visible" | "hidden") {
  Object.defineProperty(document, "visibilityState", {
    value: state,
    configurable: true,
  });
}

/** The element the feed scrolls in. */
const feed = (wrapper: ReturnType<typeof mount>) =>
  wrapper.find(".chat-feed__list").element as HTMLElement;

/**
 * Puts the reader well above the end of the correspondence. jsdom lays nothing
 * out and reports all three numbers as zero, which reads as "at the end", so
 * the position this page measures is written by hand.
 */
function parkAboveTheTail(wrapper: ReturnType<typeof mount>) {
  const el = feed(wrapper);
  Object.defineProperty(el, "scrollHeight", {
    value: 1000,
    configurable: true,
  });
  Object.defineProperty(el, "clientHeight", { value: 300, configurable: true });
  Object.defineProperty(el, "scrollTop", {
    value: 0,
    writable: true,
    configurable: true,
  });
}

/**
 * The reader standing at the end, with a feed tall enough to scroll. Without
 * the heights jsdom answers zero everywhere, and "was the feed taken down to
 * the new message" cannot be told from "there was nowhere to take it".
 */
function parkAtTheTail(wrapper: ReturnType<typeof mount>) {
  const el = feed(wrapper);
  Object.defineProperty(el, "scrollHeight", {
    value: 1000,
    configurable: true,
  });
  Object.defineProperty(el, "clientHeight", { value: 300, configurable: true });
  Object.defineProperty(el, "scrollTop", {
    value: 700,
    writable: true,
    configurable: true,
  });
}

/**
 * The reader scrolling back down to the end. The page reads the position off
 * the element, so it is written there, and the event the browser sends on the
 * way is dispatched after it.
 */
function returnToTheTail(wrapper: ReturnType<typeof mount>) {
  const el = feed(wrapper);
  el.scrollTop = 700;
  el.dispatchEvent(new Event("scroll"));
}

beforeEach(() => {
  vi.clearAllMocks();
  // clearAllMocks leaves queued one-shot answers behind, and the message page
  // is queued per test: one left unconsumed by a failing test would be served
  // to the next one and read as its own answer.
  api.getMessages.mockReset();
  hub.handlers.clear();
  hub.connected.value = false;
  // A test that hides the tab shadows this on the document itself, and the
  // shadow outlives it.
  setVisibility("visible");
  // jsdom has neither, and the message list is virtualized behind both.
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
  localStorage.clear();
  pinia = createPinia();
  setActivePinia(pinia);
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
  useAuthStore().updateUser({ username: "Alice" } as never);
  // A correspondence the reader may write in, unless a test says otherwise.
  api.getChat.mockResolvedValue({
    data: { id: "c1", participants: [], viewer: { canWrite: true } },
    error: null,
  });
  api.getMessages.mockResolvedValue(messagePage([]));
  api.markAsRead.mockResolvedValue({ data: null, error: null });
  nav.route.params = { id: "c1" };
  nav.route.query = {};
  nav.push.mockImplementation(follow);
  nav.replace.mockImplementation(follow);
});

/**
 * One page of messages as the API serves it: oldest first, newest last.
 * `hasNext` says there is history below this page, which is what tells the
 * store the reader is standing in the middle of the correspondence.
 */
function messagePage(ids: string[], { hasNext = false } = {}) {
  return {
    data: {
      resources: ids.map((id) => ({
        id,
        text: id,
        createdUtc: "2026-07-01T10:00:00Z",
        likes: [],
      })),
      paging: {
        prevCursor: null,
        nextCursor: null,
        hasPrev: false,
        hasNext,
      },
    },
    error: null,
  };
}

/** The message the API answers a send with. */
function sentMessage(id: string) {
  return {
    data: { id, text: id, createdUtc: "2026-07-01T10:00:02Z", likes: [] },
    error: null,
  };
}

function pushNewMessage(chatId: string) {
  hub.handlers.forEach((handler) =>
    handler({
      id: "n1",
      eventType: NotificationType.NewMessage,
      payload: { chatId, messageId: "m2" },
    }),
  );
}

const shownIds = () => useMessagingStore().messagesList.map((m) => m.id);

// A private message does not render [mod]: the composer offered its button
// and drew the moderator block in the preview, while the reader got the tag
// as text.
describe("ChatView, the tag set of a private message", () => {
  it("gives the composer the tags of a private message", async () => {
    const wrapper = await openChat();
    expect(
      wrapper.findComponent({ name: "BBCodeEditor" }).props("context"),
    ).toBe("direct");
  });
});

// The page mounts before the correspondence has loaded, and the feed stands
// inside it. Its markup was set up on mount all the same, on an element that
// was not there yet, and every visit left a warning about it in the console.
describe("ChatView, the markup of the feed", () => {
  it("is set up only once the feed is on screen", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const nullContainer = () =>
      warn.mock.calls.filter((args) =>
        String(args[0]).includes("null container"),
      );
    // The correspondence is held open, so the page mounts with no feed yet,
    // the way it does over a real connection.
    const chat = heldAnswer<unknown>();
    api.getChat.mockReturnValue(chat.answer);
    api.getMessages.mockResolvedValue(messagePage(["m1", "m2"]));
    try {
      const wrapper = await openChat();
      expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(true);
      expect(nullContainer()).toEqual([]);

      chat.settle({ data: { id: "c1", participants: [] }, error: null });
      await flushPromises();
      await nextTick();

      expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(false);
      expect(nullContainer()).toEqual([]);
    } finally {
      warn.mockRestore();
    }
  });
});

/**
 * The correspondence is the global chat with another line at the top of the
 * frame: who it is with and whether he is on the site, "поиск" and "…". No
 * heading of its own under the section's, no box with "← Назад", and the
 * feed's words for every state, never "Переписка не найдена" while it is still
 * loading.
 */
describe("ChatView, the correspondence in the global chat's dress", () => {
  const withBob = {
    data: {
      id: "c1",
      participants: [{ username: "Alice" }, { username: "Bob" }],
    },
    error: null,
  };

  it("leaves the one heading to the section", async () => {
    api.getChat.mockResolvedValue(withBob);
    const wrapper = await openChat();

    expect(wrapper.find("page-title").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Назад");
  });

  it("names the other one, his status, the search and the menu at the top of the frame", async () => {
    api.getChat.mockResolvedValue(withBob);
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();

    const strip = wrapper.get(".chat-strip");
    expect(strip.text().replace(/\s+/g, " ")).toBe("Bob [offline] | поиск | …");
    const name = strip.get("a.line-name");
    expect(JSON.parse(name.attributes("data-to")!)).toEqual({
      name: "profile",
      params: { username: "Bob" },
    });
    expect(strip.find(".chat-search-row").exists()).toBe(false);
  });

  it("says the other one is online while he is", async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        participants: [
          { username: "Alice" },
          { username: "Bob", lastActivityUtc: new Date().toISOString() },
        ],
      },
      error: null,
    });
    const wrapper = await openChat();

    expect(wrapper.get(".chat-strip .online").text()).toBe("online");
    expect(wrapper.find(".chat-strip .offline").exists()).toBe(false);
  });

  it("names a group by its title and the number of its participants", async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        type: "Group",
        title: "Партия",
        participants: [
          { username: "Alice" },
          { username: "Bob" },
          { username: "Eve" },
        ],
        viewer: { canWrite: true, canLeave: true, canClearHistory: true },
      },
      error: null,
    });
    const wrapper = await openChat();

    const strip = wrapper.get(".chat-strip");
    expect(strip.text().replace(/\s+/g, " ")).toBe("Партия, участников: 3 | …");
    expect(strip.find("a.line-name").exists()).toBe(false);
  });

  it("says neither missing nor empty while the correspondence is on its way", async () => {
    const chat = heldAnswer<unknown>();
    api.getChat.mockReturnValue(chat.answer);
    const wrapper = await openChat();

    expect(wrapper.text()).not.toContain("Переписка не найдена");
    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(true);
    chat.settle(withBob);
  });

  it("says neither while the first window is on its way", async () => {
    const window = heldAnswer<unknown>();
    api.getMessages.mockReturnValue(window.answer);
    const wrapper = await openChat();

    expect(wrapper.text()).not.toContain("Переписка не найдена");
    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(true);
    window.settle(messagePage([]));
  });

  it("says it is empty once the server says so", async () => {
    const wrapper = await openChat();

    expect(wrapper.text()).toContain("Сообщений пока нет");
  });

  it("calls it missing on the server's answer, with the way to the others", async () => {
    api.getChat.mockResolvedValue({ data: null, error: problem(404) });
    const wrapper = await openChat();

    expect(wrapper.text()).toContain("Переписка не найдена");
    const back = wrapper.get("a");
    expect(back.text()).toBe("Все переписки");
    // The list beside it stays on its page; the address here names none.
    expect(JSON.parse(back.attributes("data-to")!)).toEqual({
      name: "messenger",
      query: {},
    });
    expect(wrapper.find(".chat-feed__list").exists()).toBe(false);
  });

  it("offers a retry when the correspondence did not come", async () => {
    api.getChat.mockResolvedValueOnce({ data: null, error: problem(500) });
    const wrapper = await openChat();

    expect(wrapper.text()).not.toContain("Переписка не найдена");
    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить переписку",
    );

    api.getChat.mockResolvedValue(withBob);
    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(api.getChat).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.get(".chat-strip").text()).toContain("Bob");
  });

  it("holds a bar where the name will be while the correspondence is on its way", async () => {
    const chat = heldAnswer<unknown>();
    api.getChat.mockReturnValue(chat.answer);
    const wrapper = await openChat();

    expect(wrapper.find(".chat-strip .line-placeholder").exists()).toBe(true);
    expect(wrapper.get(".chat-strip").text()).toBe("");
    chat.settle(withBob);
  });

  it("offers a retry when its messages did not come, and reads none of them", async () => {
    api.getMessages.mockResolvedValueOnce({
      data: null,
      error: problem(500),
    });
    const wrapper = await openChat();

    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить сообщения",
    );
    expect(api.markAsRead).not.toHaveBeenCalled();

    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(shownIds()).toEqual(["m1"]);
    expect(api.markAsRead).toHaveBeenCalledWith("c1");
  });
});

/**
 * The search of the global chat and "Перейти к дате" open under the line of
 * the correspondence, from its "поиск", and the same logic of the history
 * window drives them: the day in the address, the line over a window nobody
 * asked for, the way back toward the latest messages.
 */
describe("ChatView, the search and the calendar", () => {
  /** A message written at noon of a day, so no zone moves it to another. */
  const dated = (id: string, day: string) => ({
    id,
    text: id,
    createdUtc: `${day}T12:00:00Z`,
    likes: [],
  });

  const datedPage = (
    messages: ReturnType<typeof dated>[],
    paging: Record<string, unknown> = {},
  ) => ({
    data: {
      resources: messages,
      paging: {
        prevCursor: null,
        nextCursor: null,
        hasPrev: false,
        hasNext: false,
        ...paging,
      },
    },
    error: null,
  });

  /** The observers of the sentinels, so a test can bring one into view. */
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
    search.searchMessages.mockReset();
  });

  const row = (wrapper: ReturnType<typeof mount>) =>
    wrapper.findComponent(ChatSearchRow);

  /** "поиск" of the line. */
  const searchItem = (wrapper: ReturnType<typeof mount>) =>
    wrapper
      .findAll(".chat-strip .strip-item")
      .find((item) => item.text() === "поиск");

  async function openRow(wrapper: ReturnType<typeof mount>) {
    await searchItem(wrapper)!.trigger("click");
    await flushPromises();
  }

  const notice = (wrapper: ReturnType<typeof mount>) =>
    wrapper.find(".chat-feed__notice");

  it('opens the row of the global chat under the line on "поиск", the caret in it', async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat({ attached: true });
    expect(row(wrapper).exists()).toBe(false);
    expect(searchItem(wrapper)!.attributes("aria-expanded")).toBe("false");

    await openRow(wrapper);

    expect(row(wrapper).props("scope")).toBe("dm:c1");
    const input = wrapper.get(".chat-search-row input");
    expect(input.attributes("placeholder")).toBe("Поиск по переписке");
    expect(document.activeElement).toBe(input.element);
    expect(searchItem(wrapper)!.attributes("aria-expanded")).toBe("true");
    // In the frame, under the line.
    expect(
      wrapper
        .find(".chat-feed__frame #conversation-search .chat-search-row")
        .exists(),
    ).toBe(true);

    await searchItem(wrapper)!.trigger("click");
    expect(row(wrapper).exists()).toBe(false);
  });

  it("opens the row on Ctrl+F", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat({ attached: true });

    const event = new KeyboardEvent("keydown", {
      key: "f",
      ctrlKey: true,
      cancelable: true,
    });
    document.dispatchEvent(event);
    await flushPromises();

    expect(event.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(
      wrapper.get(".chat-search-row input").element,
    );
  });

  it("offers no search in a correspondence with nothing in it", async () => {
    const wrapper = await openChat();

    expect(wrapper.text()).toContain("Сообщений пока нет");
    expect(searchItem(wrapper)).toBeUndefined();
    const event = new KeyboardEvent("keydown", {
      key: "f",
      ctrlKey: true,
      cancelable: true,
    });
    document.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(false);
  });

  it("offers the search while the first window is on its way", async () => {
    const window = heldAnswer<unknown>();
    api.getMessages.mockReturnValue(window.answer);
    const wrapper = await openChat();

    expect(searchItem(wrapper)).toBeDefined();
    window.settle(messagePage(["m1"]));
  });

  it("lands the feed on a hit of the search", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    search.searchMessages.mockResolvedValue({
      data: {
        resources: [
          {
            sourceType: "chat",
            sourceId: "c1",
            sourceTitle: null,
            id: "m5",
            createdUtc: "2026-07-01T10:00:00Z",
            snippet: [{ text: "пятое", isMatch: true }],
          },
        ],
        paging: {
          nextCursor: null,
          prevCursor: null,
          hasNext: false,
          hasPrev: false,
        },
      },
      error: null,
    });
    const wrapper = await openChat();
    await openRow(wrapper);

    const input = wrapper.find(".chat-search-row input");
    await input.setValue("пятое");
    await input.trigger("keydown", { key: "Enter" });
    await flushPromises();
    expect(search.searchMessages.mock.calls[0][0]).toMatchObject({
      in: ["dm:c1"],
    });

    api.getMessages.mockResolvedValue(messagePage(["m4", "m5", "m6"]));
    await wrapper.get(".result-row").trigger("click");
    await flushPromises();

    expect(api.getMessages).toHaveBeenLastCalledWith(
      "c1",
      expect.objectContaining({ aroundMessageId: "m5" }),
    );
    expect(shownIds()).toEqual(["m4", "m5", "m6"]);
  });

  it("puts a picked day in the address and opens the feed on it", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();
    await openRow(wrapper);

    api.getMessages.mockResolvedValue(
      datedPage([dated("a", "2026-09-09"), dated("b", "2026-09-10")], {
        hasNext: true,
        nextCursor: "after-b",
      }),
    );
    row(wrapper).vm.$emit("date-picked", "2026-09-10");
    await flushPromises();

    expect(nav.push).toHaveBeenCalledWith({
      name: "chat",
      params: { id: "c1" },
      query: { date: "2026-09-10" },
    });
    expect(api.getMessages).toHaveBeenLastCalledWith(
      "c1",
      expect.objectContaining({
        nearTimestampUtc: fromSiteInput("2026-09-10T00:00"),
      }),
    );
    expect(shownIds()).toEqual(["a", "b"]);
    expect(row(wrapper).props("selectedDate")).toBe("2026-09-10");
    expect(notice(wrapper).exists()).toBe(false);
  });

  it("opens on the day the address names", async () => {
    nav.route.query = { date: "2026-09-10" };
    api.getMessages.mockResolvedValue(
      datedPage([dated("b", "2026-09-10")], { hasPrev: true }),
    );
    const wrapper = await openChat();
    await openRow(wrapper);

    expect(api.getMessages).toHaveBeenCalledTimes(1);
    expect(api.getMessages.mock.calls[0][1]).toHaveProperty("nearTimestampUtc");
    expect(row(wrapper).props("selectedDate")).toBe("2026-09-10");
  });

  it("stands near a day with no message of its own, the day kept, as the global chat does", async () => {
    nav.route.query = { date: "2026-09-10" };
    api.getMessages.mockResolvedValueOnce(
      datedPage([dated("before", "2026-09-05"), dated("later", "2026-09-20")]),
    );
    const wrapper = await openChat();
    await openRow(wrapper);

    expect(notice(wrapper).exists()).toBe(false);
    expect(shownIds()).toEqual(["before", "later"]);
    expect(nav.replace).not.toHaveBeenCalled();
    expect(row(wrapper).props("selectedDate")).toBe("2026-09-10");
    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("says a linked message is gone over the latest messages", async () => {
    nav.route.query = { msg: "gone" };
    api.getMessages
      .mockResolvedValueOnce(messagePage([]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    const wrapper = await openChat();

    expect(notice(wrapper).text()).toBe(
      "Сообщение по ссылке не найдено или удалено, показаны последние сообщения",
    );
    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(shownIds()).toEqual(["m1", "m2"]);
  });

  it("offers the days from the first message, known from a window that reaches it", async () => {
    api.getMessages.mockResolvedValue(
      datedPage([dated("first", "2026-08-01"), dated("b", "2026-09-10")]),
    );
    const wrapper = await openChat();
    await openRow(wrapper);

    expect(row(wrapper).props("minDate")).toBe("2026-08-01");
    row(wrapper).vm.$emit("date-open");
    await flushPromises();
    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("asks for the first message when the calendar opens over a longer history", async () => {
    api.getMessages.mockResolvedValue(
      datedPage([dated("b", "2026-09-10")], { hasPrev: true }),
    );
    const wrapper = await openChat();
    await openRow(wrapper);
    expect(row(wrapper).props("minDate")).toBe("");

    api.getMessages.mockResolvedValue(
      datedPage([dated("first", "2025-01-15")]),
    );
    row(wrapper).vm.$emit("date-open");
    await flushPromises();

    expect(api.getMessages).toHaveBeenLastCalledWith("c1", {
      nearTimestampUtc: new Date(0).toISOString(),
      limit: 1,
    });
    expect(row(wrapper).props("minDate")).toBe("2025-01-15");
  });

  /**
   * The page stays when another correspondence is opened beside the list, and
   * the first day asked of the one before is not the first day of this one:
   * the calendar would close the days before it.
   */
  it("does not offer the first day of the correspondence opened before", async () => {
    api.getMessages.mockResolvedValue(
      datedPage([dated("b", "2026-09-10")], { hasPrev: true }),
    );
    const wrapper = await openChat();
    await openRow(wrapper);
    api.getMessages.mockResolvedValue(
      datedPage([dated("first", "2026-08-01")]),
    );
    row(wrapper).vm.$emit("date-open");
    await flushPromises();
    expect(row(wrapper).props("minDate")).toBe("2026-08-01");

    api.getChat.mockResolvedValue({
      data: { id: "c2", participants: [], viewer: { canWrite: true } },
      error: null,
    });
    api.getMessages.mockResolvedValue(
      datedPage([dated("y", "2026-09-12")], { hasPrev: true }),
    );
    nav.route.params = { id: "c2" };
    await flushPromises();
    await openRow(wrapper);
    expect(row(wrapper).props("minDate")).toBe("");

    api.getMessages.mockResolvedValue(
      datedPage([dated("first-of-c2", "2025-03-01")]),
    );
    row(wrapper).vm.$emit("date-open");
    await flushPromises();

    expect(api.getMessages).toHaveBeenLastCalledWith("c2", {
      nearTimestampUtc: new Date(0).toISOString(),
      limit: 1,
    });
    expect(row(wrapper).props("minDate")).toBe("2025-03-01");
  });

  /**
   * The list of conversations beside the feed keeps its page in the address
   * (listPage.ts): a day picked and the way back to the latest messages carry
   * it, or the list went back to its first page under the reader.
   */
  it("keeps the page of the list when a day is picked and left", async () => {
    nav.route.query = { number: "2" };
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();
    await openRow(wrapper);

    api.getMessages.mockResolvedValue(
      datedPage([dated("a", "2026-09-10")], {
        hasNext: true,
        nextCursor: "after-a",
      }),
    );
    row(wrapper).vm.$emit("date-picked", "2026-09-10");
    await flushPromises();

    expect(nav.push).toHaveBeenLastCalledWith({
      name: "chat",
      params: { id: "c1" },
      query: { number: "2", date: "2026-09-10" },
    });

    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    wrapper.findComponent({ name: "ChatFeed" }).vm.$emit("jump-to-latest");
    await flushPromises();

    expect(nav.replace).toHaveBeenLastCalledWith({
      name: "chat",
      params: { id: "c1" },
      query: { number: "2" },
    });
  });

  /**
   * From a day back toward the latest messages: the feed pages down as the
   * reader reaches the bottom of what is loaded. The correspondence had no
   * way down at all, and the sentinel of a window drawn anew was never
   * watched.
   */
  it("pages from a picked day toward the latest messages", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();
    await openRow(wrapper);

    api.getMessages.mockResolvedValue(
      datedPage([dated("a", "2026-09-10")], {
        hasNext: true,
        nextCursor: "after-a",
      }),
    );
    row(wrapper).vm.$emit("date-picked", "2026-09-10");
    await flushPromises();
    // The landing scroll settles before the sentinels wake.
    await new Promise((resolve) => setTimeout(resolve, 650));

    const sentinels = wrapper.findAll(".chat-feed__sentinel");
    const bottom = sentinels[sentinels.length - 1].element;
    const watching = observers.filter((o) => o.targets.includes(bottom));
    expect(watching.length).toBeGreaterThan(0);

    api.getMessagesAfter.mockResolvedValue(
      datedPage([dated("b", "2026-09-11")]),
    );
    watching
      .at(-1)!
      .callback(
        [{ isIntersecting: true } as IntersectionObserverEntry],
        {} as IntersectionObserver,
      );
    await flushPromises();

    expect(api.getMessagesAfter).toHaveBeenCalledWith(
      "c1",
      "after-a",
      expect.any(Number),
    );
    expect(shownIds()).toEqual(["a", "b"]);
  });
});

// The toolbar over a message goes away the moment the feed scrolls, or it
// stands over whatever message slides under it. The handlers were added on
// mount, before the correspondence had loaded and the feed existed, so they
// never ran at all.
describe("ChatView, scrolling the feed", () => {
  async function openLate() {
    const chat = heldAnswer<unknown>();
    api.getChat.mockReturnValue(chat.answer);
    api.getMessages.mockResolvedValue(messagePage(["m1", "m2"]));
    const wrapper = await openChat();
    chat.settle({ data: { id: "c1", participants: [] }, error: null });
    await flushPromises();
    await nextTick();
    return wrapper;
  }

  it("takes the toolbar away on a scroll of a feed that loaded after the page", async () => {
    const wrapper = await openLate();
    toolbar.last!.hoveredMessageId.value = "m1";

    await wrapper.get(".chat-feed__list").trigger("scroll");

    expect(toolbar.last!.hoveredMessageId.value).toBeNull();
  });

  it("takes the toolbar away on a turn of the wheel over it as well", async () => {
    const wrapper = await openLate();
    toolbar.last!.hoveredMessageId.value = "m1";

    await wrapper.get(".chat-feed__list").trigger("wheel");

    expect(toolbar.last!.hoveredMessageId.value).toBeNull();
  });
});

/**
 * A refused send used to take the message with it. The field was emptied and
 * the editor cleared before the request went out, and the store answered with
 * the created message or with null — a shape that says nothing about why there
 * is no message. So a 400 on length, a blacklisted recipient or a dropped
 * connection all ended the same way: an empty field, an empty correspondence,
 * and no draft in localStorage either, because clear() removes that too.
 *
 * These pin the bargain the composer offers instead. The field empties at once
 * — that is what makes sending feel instant — and everything is handed back
 * when the send does not land: the text, the draft, and a sentence saying so.
 */
describe("ChatView, sending a message", () => {
  it("keeps the text, the draft and names the reason when the send fails", async () => {
    api.sendMessage.mockResolvedValue({ data: null, error: problem(400) });
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("длинное письмо");
    await wrapper.find(".chat-composer__send").trigger("click");
    await flushPromises();
    await nextTick();

    expect(api.sendMessage).toHaveBeenCalledWith("c1", "длинное письмо");
    expect(field(wrapper).value).toBe("длинное письмо");
    // clear() drops the saved draft, so it may not run before the send lands:
    // the draft is the only copy that outlives the tab.
    expect(clearSpy).not.toHaveBeenCalled();
    // Under the "Отправить" that sent it, and in no plate.
    expect(wrapper.get(".chat-composer__send + .action-result").text()).toBe(
      "Не удалось отправить сообщение",
    );
    expect(useToast().toasts.value.map((t) => t.message)).toEqual([]);
  });

  it("empties the field and drops the draft once the send lands", async () => {
    api.sendMessage.mockResolvedValue({
      data: {
        id: "m1",
        text: "привет",
        createdUtc: "2026-07-01T10:00:00Z",
        likes: [],
      },
      error: null,
    });
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("привет");
    await wrapper.find(".chat-composer__send").trigger("click");
    await flushPromises();
    await nextTick();

    expect(field(wrapper).value).toBe("");
    expect(clearSpy).toHaveBeenCalledTimes(1);
    expect(useToast().toasts.value).toHaveLength(0);
  });

  it("sends nothing on a field holding only spaces", async () => {
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("   ");
    // Through the editor's own submit (Ctrl+Enter), which the disabled send
    // button would otherwise hide: the guard is in the handler.
    wrapper.findComponent({ name: "BBCodeEditor" }).vm.$emit("submit");
    await flushPromises();

    expect(api.sendMessage).not.toHaveBeenCalled();
    expect(field(wrapper).value).toBe("   ");
    expect(useToast().toasts.value).toHaveLength(0);
  });
});

/**
 * The field is offered only where the server says the message would be taken
 * (owner, 05.10: what may not be done is not offered). The chat answers it as
 * `viewer.canWrite`, asked by the code that refuses the message: the other side
 * of a conversation of two may keep the reader on a blacklist with private
 * messages closed, and the reader used to type the message and be told after
 * it that it could not be sent.
 */
describe("ChatView, the field by the server's answer", () => {
  it("has no field where the server says the message would be refused", async () => {
    api.getChat.mockResolvedValue({
      data: { id: "c1", participants: [], viewer: { canWrite: false } },
      error: null,
    });
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();

    expect(wrapper.find(".chat-feed__list").exists()).toBe(true);
    expect(wrapper.findComponent({ name: "ChatComposer" }).exists()).toBe(
      false,
    );
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    // Nothing stands in its place: no line saying why.
    expect(wrapper.get(".chat-feed__composer").text()).toBe("");
  });

  it("has no field on an answer that says nothing about the reader", async () => {
    api.getChat.mockResolvedValue({
      data: { id: "c1", participants: [] },
      error: null,
    });
    const wrapper = await openChat();

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  it("offers no field while another correspondence is on its way", async () => {
    const wrapper = await openChat();
    expect(wrapper.find(".editor-stub").exists()).toBe(true);

    const next = heldAnswer<unknown>();
    api.getChat.mockReturnValueOnce(next.answer);
    nav.route.params = { id: "c2" };
    await flushPromises();

    // The answer about the correspondence left behind is not this one's.
    expect(wrapper.find(".editor-stub").exists()).toBe(false);

    next.settle({
      data: { id: "c2", participants: [], viewer: { canWrite: true } },
      error: null,
    });
    await flushPromises();

    expect(wrapper.find(".editor-stub").exists()).toBe(true);
  });
});

/**
 * The open correspondence used to be a snapshot: whatever the page fetched on
 * mount stayed on screen until it was reopened, while the socket delivered the
 * message that had just arrived to the badge and nowhere else.
 *
 * These pin the contract the hub is read under (API_DESIGN.md, "Realtime"):
 * the push is only a hint to go and read, the read is deduplicated by id so a
 * repeat changes nothing, and a tail that does not overlap what is on screen
 * is a gap — the window is re-read whole rather than stitched over it.
 */
describe("ChatView, live tail", () => {
  it("leaves the feed alone on a push about another correspondence", async () => {
    api.getMessages.mockResolvedValueOnce(messagePage(["m1"]));
    await openChat();
    expect(api.getMessages).toHaveBeenCalledTimes(1);

    pushNewMessage("c2");
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(1);
    expect(shownIds()).toEqual(["m1"]);
  });

  it("reads the tail on a push about the open one and repeats nothing", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();

    pushNewMessage("c1");
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(shownIds()).toEqual(["m1", "m2"]);
  });

  it("re-reads the window whole when the tail does not overlap it", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m9", "m10"]))
      .mockResolvedValueOnce(messagePage(["m8", "m9", "m10"]));
    await openChat();

    pushNewMessage("c1");
    await flushPromises();

    // The third call is the re-read; m1 is gone from the window instead of
    // sitting above a hole.
    expect(api.getMessages).toHaveBeenCalledTimes(3);
    expect(shownIds()).toEqual(["m8", "m9", "m10"]);
  });

  /**
   * The window is the page the reader is reading, and a gap bigger than a page
   * used to replace it wherever he stood: a reader up in the history was handed
   * the end of a correspondence he had not read, with the message he was on
   * gone from the screen and nothing said about it.
   */
  it("leaves the window in place under a reader who has paged up", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m9", "m10"]));
    const wrapper = await openChat();
    parkAboveTheTail(wrapper);
    const el = feed(wrapper);

    pushNewMessage("c1");
    await flushPromises();

    // The tail was read and nothing came of it: no re-read, no new window, and
    // the reader is where he was.
    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(shownIds()).toEqual(["m1"]);
    expect(el.scrollTop).toBe(0);
  });

  it("re-reads the window when the reader comes back down to the end", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m9", "m10"]))
      .mockResolvedValueOnce(messagePage(["m9", "m10"]))
      .mockResolvedValueOnce(messagePage(["m8", "m9", "m10"]));
    const wrapper = await openChat();
    parkAboveTheTail(wrapper);

    pushNewMessage("c1");
    await flushPromises();
    expect(shownIds()).toEqual(["m1"]);

    returnToTheTail(wrapper);
    await flushPromises();

    // The tail is read again rather than the answer kept from then: what the
    // deferred read found is as old as the moment he started scrolling back.
    expect(api.getMessages).toHaveBeenCalledTimes(4);
    expect(shownIds()).toEqual(["m8", "m9", "m10"]);
  });

  it("reads the tail when the tab comes back on screen", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();

    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    expect(shownIds()).toEqual(["m1", "m2"]);
  });

  it("reads the tail when the socket comes back up", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();

    hub.connected.value = true;
    await nextTick();
    await flushPromises();

    expect(shownIds()).toEqual(["m1", "m2"]);
  });

  it("stops listening once the correspondence is closed", async () => {
    api.getMessages.mockResolvedValueOnce(messagePage(["m1"]));
    await openChat();

    closeChat();
    await flushPromises();

    expect(hub.handlers.size).toBe(0);
    pushNewMessage("c1");
    await flushPromises();
    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("lets go of the tab listener once the correspondence is closed", async () => {
    api.getMessages.mockResolvedValueOnce(messagePage(["m1"]));
    await openChat();
    const removeListener = vi.spyOn(document, "removeEventListener");

    closeChat();
    await flushPromises();

    expect(removeListener).toHaveBeenCalledWith(
      "visibilitychange",
      expect.any(Function),
    );
    removeListener.mockRestore();
  });

  it("reads nothing when the tab goes off screen", async () => {
    api.getMessages.mockResolvedValueOnce(messagePage(["m1"]));
    await openChat();

    setVisibility("hidden");
    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("reads no tail for a reader standing in the middle of the history", async () => {
    // hasNext: the window ends before the newest message, so the reader is
    // parked somewhere above the tail.
    api.getMessages.mockResolvedValueOnce(
      messagePage(["m1"], { hasNext: true }),
    );
    await openChat();

    pushNewMessage("c1");
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(1);
    expect(shownIds()).toEqual(["m1"]);
  });

  it("drops a tail that landed after the reader left the end of the history", async () => {
    const held = heldAnswer<ReturnType<typeof messagePage>>();
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockReturnValueOnce(held.answer)
      .mockResolvedValueOnce(messagePage(["m8", "m9", "m10"]));
    await openChat();

    pushNewMessage("c1");
    await flushPromises();
    // While the tail was in flight the reader landed on a message found in
    // search, or paged up far enough for the window to be trimmed. Either way
    // the window they are reading no longer ends at the newest message.
    useMessagingStore().hasMoreAfter = true;
    held.settle(messagePage(["m9", "m10"]));
    await flushPromises();

    // No third request: this tail does not overlap the window, and the answer
    // to that is a full re-read, which would take the reader to the end of the
    // correspondence they had just left.
    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(shownIds()).toEqual(["m1"]);
  });

  it("puts the overlap in the order the server holds it", async () => {
    api.sendMessage.mockResolvedValue(sentMessage("mine"));
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "theirs", "mine"]));
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("привет");
    await wrapper.find(".chat-composer__send").trigger("click");
    await flushPromises();
    // The store puts the sent message at the end of the list as soon as the
    // POST answers, whatever was written elsewhere in the meantime.
    expect(shownIds()).toEqual(["m1", "mine"]);

    pushNewMessage("c1");
    await flushPromises();

    expect(shownIds()).toEqual(["m1", "theirs", "mine"]);
  });

  it("waits for a send in flight and then reads the tail once", async () => {
    const held = heldAnswer<ReturnType<typeof sentMessage>>();
    api.sendMessage.mockReturnValue(held.answer);
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "mine"]));
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("привет");
    await wrapper.find(".chat-composer__send").trigger("click");
    await flushPromises();

    pushNewMessage("c1");
    await flushPromises();
    // Reading now would bring the sent message in ahead of the store, which
    // appends it again the moment the POST answers.
    expect(api.getMessages).toHaveBeenCalledTimes(1);

    held.settle(sentMessage("mine"));
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(shownIds()).toEqual(["m1", "mine"]);
  });
});

/**
 * The badge in the header and the open correspondence answer the same push:
 * App.vue re-reads the unread counter while this page appends the message. Left
 * at that, the header claimed an unread message over the very message the
 * reader was looking at, until the correspondence was reopened.
 */
describe("ChatView, read receipt for the live tail", () => {
  it("marks the correspondence read when a message arrives in a visible tab", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();
    // Opening marks it read as well; what is counted here is the receipt for
    // the message that arrived afterwards.
    api.markAsRead.mockClear();

    pushNewMessage("c1");
    await flushPromises();

    expect(shownIds()).toEqual(["m1", "m2"]);
    expect(api.markAsRead).toHaveBeenCalledTimes(1);
    expect(api.markAsRead).toHaveBeenCalledWith("c1");
  });

  /**
   * The message was appended to the end of a feed that did not follow it, and
   * the receipt went out regardless of whether the reader could see it. Either
   * half alone is wrong: a feed that follows without a receipt leaves the badge
   * over a message on screen, and a receipt without the feed following clears
   * the unread mark for a message nobody has seen. So one measurement, taken
   * before the list grows, decides both.
   */
  it("takes the feed down to the message and marks it read for a reader at the end", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    const wrapper = await openChat();
    api.markAsRead.mockClear();
    parkAtTheTail(wrapper);
    virtual.scrollToIndex.mockClear();

    pushNewMessage("c1");
    await flushPromises();
    await nextTick();

    expect(shownIds()).toEqual(["m1", "m2"]);
    // The date of the two and the two messages: the last row is the third.
    expect(virtual.scrollToIndex).toHaveBeenLastCalledWith(2, {
      align: "end",
    });
    expect(api.markAsRead).toHaveBeenCalledTimes(1);
  });

  it("moves neither the reader nor the unread mark above the end of the history", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    const wrapper = await openChat();
    api.markAsRead.mockClear();
    parkAboveTheTail(wrapper);
    const el = feed(wrapper);
    virtual.scrollToIndex.mockClear();

    pushNewMessage("c1");
    await flushPromises();
    await nextTick();

    // The message is in the feed and below the fold. The reader was reading
    // something else, and the unread mark is the one sign it arrived.
    expect(shownIds()).toEqual(["m1", "m2"]);
    expect(el.scrollTop).toBe(0);
    expect(virtual.scrollToIndex).not.toHaveBeenCalled();
    expect(api.markAsRead).not.toHaveBeenCalled();
  });

  it("holds the receipt while the tab is hidden and sends it on the way back", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();
    api.markAsRead.mockClear();
    setVisibility("hidden");

    pushNewMessage("c1");
    await flushPromises();

    // The message is in the feed, and nobody has looked at it yet.
    expect(shownIds()).toEqual(["m1", "m2"]);
    expect(api.markAsRead).not.toHaveBeenCalled();

    setVisibility("visible");
    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    // The return to the tab reads the tail, and this time the receipt goes
    // with it even though that read found nothing left to append.
    expect(api.getMessages).toHaveBeenCalledTimes(3);
    expect(api.markAsRead).toHaveBeenCalledTimes(1);
    expect(api.markAsRead).toHaveBeenCalledWith("c1");
  });
});

/**
 * "…" of the line: the menu of the correspondence. Each line stands where the
 * server says the reader may do it; the clearing and the leaving ask first,
 * and the window runs the request and prints a refusal in itself.
 */
describe("ChatView, the menu of the correspondence", () => {
  const trigger = (wrapper: ReturnType<typeof mount>) =>
    wrapper.find('.chat-strip button[aria-label="Действия с перепиской"]');

  async function openMenu(wrapper: ReturnType<typeof mount>) {
    await trigger(wrapper).trigger("click");
    await nextTick();
  }

  const items = (wrapper: ReturnType<typeof mount>) =>
    wrapper.findAll('[role="menuitem"]').map((item) => item.text());

  const direct = (viewer: Record<string, boolean>) => ({
    data: {
      id: "c1",
      type: "Direct",
      participants: [{ username: "Alice" }, { username: "Bob" }],
      viewer: { canWrite: true, ...viewer },
    },
    error: null,
  });

  it("leads to the other one's profile and clears the history of a conversation of two", async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    const wrapper = await openChat();

    expect(wrapper.find('[role="menu"]').exists()).toBe(false);
    await openMenu(wrapper);

    expect(trigger(wrapper).attributes("aria-expanded")).toBe("true");
    expect(items(wrapper)).toEqual(["Перейти в профиль", "Очистить историю"]);
    expect(
      JSON.parse(wrapper.get('a[role="menuitem"]').attributes("data-to")!),
    ).toEqual({ name: "profile", params: { username: "Bob" } });
  });

  it("offers only what the server allows", async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: false }));
    const wrapper = await openChat();
    await openMenu(wrapper);

    expect(items(wrapper)).toEqual(["Перейти в профиль"]);
  });

  it("offers the leaving of a group, and no profile in it", async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        type: "Group",
        title: "Партия",
        participants: [{ username: "Alice" }, { username: "Bob" }],
        viewer: { canWrite: true, canLeave: true, canClearHistory: true },
      },
      error: null,
    });
    const wrapper = await openChat();
    await openMenu(wrapper);

    expect(items(wrapper)).toEqual(["Очистить историю", "Покинуть группу"]);
  });

  it('has no "…" where there is nothing to do', async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        type: "Group",
        title: "Партия",
        participants: [{ username: "Alice" }],
        viewer: { canWrite: true },
      },
      error: null,
    });
    const wrapper = await openChat();

    expect(trigger(wrapper).exists()).toBe(false);
  });

  it('closes on Escape and gives the focus back to "…"', async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    const wrapper = await openChat({ attached: true });
    await openMenu(wrapper);

    await wrapper.get(".menu-anchor").trigger("keydown", { key: "Escape" });

    expect(wrapper.find('[role="menu"]').exists()).toBe(false);
    expect(document.activeElement).toBe(trigger(wrapper).element);
  });

  /**
   * The menu is drawn at the end of the page, so Tab from "…" goes on into
   * the feed, not into the menu: the menu goes when the focus leaves "…" and
   * the menu both, and stays while it moves between them. A press on what
   * takes no focus moves it nowhere, and the click is the one to answer it.
   */
  it('closes when the focus leaves "…" and the menu', async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    const wrapper = await openChat({ attached: true });
    await openMenu(wrapper);
    const leave = (from: Element, to: Element | null) =>
      from.dispatchEvent(
        new FocusEvent("focusout", { bubbles: true, relatedTarget: to }),
      );
    const item = wrapper.get('button[role="menuitem"]').element;

    leave(trigger(wrapper).element, item);
    leave(item, null);
    await nextTick();
    expect(wrapper.find('[role="menu"]').exists()).toBe(true);

    leave(item, feed(wrapper));
    await nextTick();
    expect(wrapper.find('[role="menu"]').exists()).toBe(false);

    await openMenu(wrapper);
    leave(trigger(wrapper).element, feed(wrapper));
    await nextTick();
    expect(wrapper.find('[role="menu"]').exists()).toBe(false);
  });

  /**
   * The menu is drawn over the page, not inside the frame of the feed, which
   * clips what stands outside it: in a narrow column it was cut at the
   * frame's edge. jsdom lays nothing out, so "…", the menu and the window
   * are given their sizes here.
   */
  describe("where the menu hangs", () => {
    let restore: (() => void) | null = null;

    afterEach(() => {
      restore?.();
      restore = null;
    });

    /** "…" at 300..310 under a line ending at 40, a menu 170 wide. */
    function lay(wrapper: ReturnType<typeof mount>, windowWidth: number) {
      const button = trigger(wrapper).element as HTMLElement;
      button.getBoundingClientRect = () =>
        ({ left: 300, right: 310, top: 24, bottom: 40 }) as DOMRect;
      const width = Object.getOwnPropertyDescriptor(
        HTMLElement.prototype,
        "offsetWidth",
      )!;
      Object.defineProperty(HTMLElement.prototype, "offsetWidth", {
        configurable: true,
        get() {
          return (this as HTMLElement).classList.contains("conversation-menu")
            ? 170
            : 0;
        },
      });
      Object.defineProperty(document.documentElement, "clientWidth", {
        configurable: true,
        value: windowWidth,
      });
      restore = () => {
        Object.defineProperty(HTMLElement.prototype, "offsetWidth", width);
        delete (document.documentElement as { clientWidth?: number })
          .clientWidth;
      };
    }

    const placed = (wrapper: ReturnType<typeof mount>) =>
      (wrapper.get('[role="menu"]').element as HTMLElement).style;

    it('stands under "…" at its left edge where the window has room', async () => {
      api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
      const wrapper = await openChat({ attached: true });
      lay(wrapper, 1000);
      await openMenu(wrapper);
      await flushPromises();

      expect(placed(wrapper).left).toBe("300px");
      expect(placed(wrapper).top).toBe("44px");
      expect(placed(wrapper).visibility).toBe("");
    });

    it('stands at the right edge of "…" where the window has no room to its right', async () => {
      api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
      const wrapper = await openChat({ attached: true });
      lay(wrapper, 420);
      await openMenu(wrapper);
      await flushPromises();

      expect(placed(wrapper).left).toBe(`${310 - 170}px`);
    });

    it("goes away when the page scrolls under it", async () => {
      api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
      const wrapper = await openChat({ attached: true });
      await openMenu(wrapper);
      await flushPromises();
      expect(wrapper.find('[role="menu"]').exists()).toBe(true);

      document.body.dispatchEvent(new Event("scroll"));
      await nextTick();

      expect(wrapper.find('[role="menu"]').exists()).toBe(false);
    });
  });

  it("asks before it clears, then empties the feed for the reader", async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    api.getMessages.mockResolvedValue(messagePage(["m1", "m2"]));
    api.clearHistory.mockResolvedValue({ data: null, error: null });
    const wrapper = await openChat();
    await openMenu(wrapper);

    const clear = wrapper
      .findAll('[role="menuitem"]')
      .find((item) => item.text() === "Очистить историю")!;
    await clear.trigger("click");
    await nextTick();

    const dialog = wrapper.get('[role="alertdialog"]');
    expect(dialog.get(".dialog-title").text()).toBe("Очистить историю?");
    expect(dialog.get(".dialog-message").text()).toBe(
      "Сообщения, написанные до этой минуты, перестанут показываться вам, а у собеседника останутся",
    );
    expect(api.clearHistory).not.toHaveBeenCalled();

    const confirm = dialog
      .findAll("button")
      .find((button) => button.text() === "Очистить")!;
    await confirm.trigger("click");
    await flushPromises();

    expect(api.clearHistory).toHaveBeenCalledWith("c1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(shownIds()).toEqual([]);
    expect(wrapper.text()).toContain("Сообщений пока нет");
    expect(wrapper.find('[role="alertdialog"]').exists()).toBe(false);
  });

  it("keeps the window open with the reason when the clearing is refused", async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    api.clearHistory.mockResolvedValue({ data: null, error: problem(500) });
    const wrapper = await openChat();
    await openMenu(wrapper);
    await wrapper
      .findAll('[role="menuitem"]')
      .find((item) => item.text() === "Очистить историю")!
      .trigger("click");
    await nextTick();

    await wrapper
      .get('[role="alertdialog"]')
      .findAll("button")
      .find((button) => button.text() === "Очистить")!
      .trigger("click");
    await flushPromises();

    expect(wrapper.get(".dialog-refusal").text()).toContain(
      "Не удалось очистить историю",
    );
    expect(shownIds()).toEqual(["m1"]);
  });

  it("asks before it leaves a group, then goes back to the list", async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        type: "Group",
        title: "Партия",
        participants: [{ username: "Alice" }, { username: "Bob" }],
        viewer: { canWrite: true, canLeave: true },
      },
      error: null,
    });
    api.leaveChat.mockResolvedValue({ data: null, error: null });
    const wrapper = await openChat();
    await openMenu(wrapper);
    await wrapper
      .findAll('[role="menuitem"]')
      .find((item) => item.text() === "Покинуть группу")!
      .trigger("click");
    await nextTick();

    const dialog = wrapper.get('[role="alertdialog"]');
    expect(dialog.get(".dialog-title").text()).toBe("Покинуть группу?");
    await dialog
      .findAll("button")
      .find((button) => button.text() === "Покинуть")!
      .trigger("click");
    await flushPromises();

    expect(api.leaveChat).toHaveBeenCalledWith("c1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(nav.push).toHaveBeenCalledWith({ name: "messenger" });
  });
});

/**
 * The link of a message in a correspondence opens the correspondence standing
 * on that message: an ordinary link, which the reader opens or copies.
 */
describe("ChatView, the link of a message", () => {
  it("leads to the correspondence on the message", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();

    const linkTo = wrapper
      .findComponent({ name: "ChatFeed" })
      .props("linkTo") as (id: string) => unknown;
    expect(linkTo("m1")).toEqual({
      name: "chat",
      params: { id: "c1" },
      query: { msg: "m1" },
    });
  });

  it("keeps the page of the list beside the correspondence", async () => {
    nav.route.query = { number: "2" };
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();

    const linkTo = wrapper
      .findComponent({ name: "ChatFeed" })
      .props("linkTo") as (id: string) => unknown;
    expect(linkTo("m1")).toEqual({
      name: "chat",
      params: { id: "c1" },
      query: { number: "2", msg: "m1" },
    });
  });
});
