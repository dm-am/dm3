/** The stand-ins of the ChatView specs, handed to vi.mock; the editor is bbcodeEditorStub's composer. */
import { vi } from "vitest";
import { reactive, ref } from "vue";

/** The messaging client, one vi.fn per call the page and its store make. */
export const api = {
  getChat: vi.fn(),
  getMessages: vi.fn(),
  markAsRead: vi.fn(),
  sendMessage: vi.fn(),
  getMessagesBefore: vi.fn(),
  getMessagesAfter: vi.fn(),
  getMessageForEdit: vi.fn(),
  clearHistory: vi.fn(),
  leaveChat: vi.fn(),
};

export const messagingApi = () => ({ default: api });

/** The clear() of the composer's editor. */
export const clearSpy = vi.fn();

export const search = { searchMessages: vi.fn() };

export const messageSearchApi = () => ({ default: search });

/** The shared socket, reduced to its handlers and its connected flag: the real one is a hub singleton. */
export const hub = {
  handlers: new Set<(notification: unknown) => void>(),
  connected: ref(false),
};

export const signalR = () => ({
  useGlobalSignalR: () => ({
    connect: vi.fn(),
    disconnect: vi.fn(),
    onNotification: (handler: (notification: unknown) => void) => {
      hub.handlers.add(handler);
      return () => hub.handlers.delete(handler);
    },
    isConnected: hub.connected,
  }),
});

/** The toolbar the page is given, the last instance kept: jsdom draws no rows of the virtual list. */
export const toolbar = {
  last: null as null | {
    hoveredMessageId: { value: string | null };
  },
};

export const messageToolbar = (
  real: typeof import("@/shared/lib/composables/useMessageToolbar"),
) => ({
  useMessageToolbar: (...args: Parameters<typeof real.useMessageToolbar>) => {
    const made = real.useMessageToolbar(...args);
    toolbar.last = made;
    return made;
  },
});

/** The virtualizer, its scroll to an item remembered: jsdom lays nothing out. */
export const virtual = { scrollToIndex: vi.fn() };

export const virtualScroll = (
  real: typeof import("@/shared/lib/composables/useVirtualScroll"),
) => ({
  useVirtualScroll: (...args: Parameters<typeof real.useVirtualScroll>) => {
    const made = real.useVirtualScroll(...args);
    return {
      ...made,
      scrollToIndex: (...call: Parameters<typeof made.scrollToIndex>): void => {
        virtual.scrollToIndex(...call);
        made.scrollToIndex(...call);
      },
    };
  },
});

/** A route the test can move; the page moves it too, and the watchers see the query change. */
export const nav = {
  route: reactive({
    params: { id: "c1" } as Record<string, string>,
    query: {} as Record<string, string>,
  }),
  push: vi.fn(),
  replace: vi.fn(),
};

export const vueRouter = () => ({
  useRoute: () => nav.route,
  useRouter: () => ({
    push: nav.push,
    replace: nav.replace,
    resolve: () => ({ href: "/messenger/c/c1" }),
  }),
});
