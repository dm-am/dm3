/**
 * @vitest-environment jsdom
 */

/**
 * Who has a field to write in.
 *
 * During a closed event the server takes messages from its participants only
 * ("Идет закрытый эвент. Писать могут только его участники.", MessageService).
 * The page used to say that in a line over a field that was open all the same,
 * so a reader could write a whole message only to have it refused. The owner,
 * 5 October 2026 (ACC-364): what a reader may not do is not on the screen; the
 * event's own line in the frame says what is going on. So outside the event
 * there is no field, no "Отправить", and no line about either; a guest keeps
 * the invitation to sign in where the field would be.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { useAuthStore } from "@/shared/stores";

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

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

vi.mock("vue-final-modal", () => ({
  useModal: () => ({ open: vi.fn(), close: vi.fn() }),
}));

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

async function openChat() {
  const wrapper = mount(GlobalChatPage, {
    shallow: true,
    global: { plugins: [pinia], stubs: { ChatFeed: false } },
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
  // The signed-in reader of a test before is kept there.
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
    // Never answered: a participant would see the field go and come back.
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
