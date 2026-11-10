/**
 * @vitest-environment jsdom
 */

// During a closed event the server takes messages from its participants only
// (MessageService), so outside it there is no field and no line about it; a
// guest keeps the invitation to sign in.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import { flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { mountPage } from "@/shared/lib/testing/mountPage";

const { api } = vi.hoisted(() => ({
  api: {
    getMessages: vi.fn(),
    getEvents: vi.fn(),
    getEvent: vi.fn(),
    getMessagesAround: vi.fn(),
    getMessagesNearDate: vi.fn(),
  },
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

import GlobalChatPage from "./GlobalChatPage.vue";

enableAutoUnmount(afterEach);

let pinia: Pinia;

const emptyPage = {
  data: {
    resources: [],
    paging: {
      prevCursor: null,
      nextCursor: null,
      hasPrev: false,
      hasNext: false,
    },
  },
  error: null,
};

const liveEvent = (isOpen: boolean) => ({
  id: "e1",
  title: "Вечер быстрых зарисовок",
  status: "Live",
  isOpen,
  startsUtc: "2026-10-05T18:00:00Z",
});

/** The details of the running event, with the participants named. */
const details = (participants: string[]) => ({
  data: {
    resource: {
      ...liveEvent(false),
      participants: participants.map((username) => ({ user: { username } })),
    },
  },
  error: null,
});

/** Every child but the feed, as a shallow mount would leave them. */
const stubs = {
  PageTitle: true,
  ChatSearchRow: true,
  ChatEventsPanel: true,
  LoginPrompt: true,
  ChatComposer: true,
  ChatStrip: true,
  FeedEdge: true,
  MessageToolbar: true,
  ChatMessage: true,
  ChatMessageSkeleton: true,
  ErrorState: true,
  DashSeparator: true,
  ActionResult: true,
  SvgIcon: true,
};

async function openChat() {
  const { wrapper } = await mountPage(GlobalChatPage, "/global-chat", {
    routes: [["global-chat", "/global-chat"]],
    pinia,
    stubs,
  });
  await flushPromises();
  await nextTick();
  return wrapper;
}

const composer = (wrapper: Awaited<ReturnType<typeof openChat>>) =>
  wrapper.findComponent({ name: "ChatComposer" });

beforeEach(() => {
  vi.clearAllMocks();
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
  // A reader signed in by an earlier test is dropped.
  localStorage.clear();
  pinia = createPinia();
  setActivePinia(pinia);
  api.getMessages.mockResolvedValue(emptyPage);
  api.getEvents.mockResolvedValue({ data: { resources: [] }, error: null });
});

describe("GlobalChatPage, the field to write in", () => {
  it("stands for a signed-in reader when no event is closed", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);

    const wrapper = await openChat();

    expect(composer(wrapper).exists()).toBe(true);
  });

  it("is not there outside a closed event, and nothing says it is not", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    api.getEvents.mockResolvedValue({
      data: { resources: [liveEvent(false)] },
      error: null,
    });
    api.getEvent.mockResolvedValue(details(["Bob"]));

    const wrapper = await openChat();

    expect(composer(wrapper).exists()).toBe(false);
    expect(wrapper.text()).not.toContain("писать могут только");
    expect(wrapper.text()).not.toContain("Отправить");
  });

  it("stands for a participant of the closed event", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    api.getEvents.mockResolvedValue({
      data: { resources: [liveEvent(false)] },
      error: null,
    });
    api.getEvent.mockResolvedValue(details(["Alice", "Bob"]));

    const wrapper = await openChat();

    expect(composer(wrapper).exists()).toBe(true);
  });

  it("stands while the participants of the closed event are on their way", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    api.getEvents.mockResolvedValue({
      data: { resources: [liveEvent(false)] },
      error: null,
    });
    // Never answered: the field stays, or a participant would see it go and come back.
    api.getEvent.mockReturnValue(new Promise(() => {}));

    const wrapper = await openChat();

    expect(composer(wrapper).exists()).toBe(true);
  });

  it("stands for everybody signed in while the event is open", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    api.getEvents.mockResolvedValue({
      data: { resources: [liveEvent(true)] },
      error: null,
    });
    api.getEvent.mockResolvedValue(details(["Bob"]));

    const wrapper = await openChat();

    expect(composer(wrapper).exists()).toBe(true);
  });

  it("gives a guest the invitation to sign in instead", async () => {
    const wrapper = await openChat();

    expect(composer(wrapper).exists()).toBe(false);
    expect(wrapper.findComponent({ name: "LoginPrompt" }).exists()).toBe(true);
  });
});
