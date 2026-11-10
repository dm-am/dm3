/** The data of the ChatView specs, the page mounted and closed, and the state every test starts from. */
import { vi } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import type { Router } from "vue-router";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import { useMessagingStore } from "@/entities/message";
import { api, hub } from "./ChatView.doubles";
import ChatView from "./ChatView.vue";

/** Not enableAutoUnmount: one test closes the page itself, and a second unmount makes Vue warn. */
let closePage: (() => void) | null = null;

let pinia: Pinia;
let router: Router;

/** The messenger's addresses and the profile the correspondence links to. */
const routes: Address[] = [
  ["messenger", "/messenger"],
  ["chat", "/messenger/c/:id"],
  ["direct-message", "/messenger/user/:username"],
  ["profile", "/users/:username"],
];

/**
 * Opens the correspondence at its address, c1 unless the test names another;
 * `attached` puts it in the document, where the focus can go.
 */
export async function openChat(
  options: { attached?: boolean; address?: string } = {},
) {
  const mounted = await mountPage(
    ChatView,
    options.address ?? "/messenger/c/c1",
    {
      routes,
      pinia,
      stubs: { Teleport: true },
      ...(options.attached ? { attachTo: document.body } : {}),
    },
  );
  router = mounted.router;
  const wrapper = mounted.wrapper;
  closePage = () => {
    closePage = null;
    wrapper.unmount();
  };
  await flushPromises();
  return wrapper;
}

/** Where the page stands now, after the moves it and the test made. */
export const address = () => router.currentRoute.value;

/** The reader goes to another address, as a followed link takes him. */
export async function moveTo(to: string) {
  await router.push(to);
  await flushPromises();
}

/** Closes the open correspondence, if a test left one open. */
export function closeChat() {
  closePage?.();
}

/** An answer held open, so a test can act while the request is in flight. */
export function heldAnswer<T>() {
  let settle!: (answer: T) => void;
  const answer = new Promise<T>((resolve) => {
    settle = resolve;
  });
  return { answer, settle };
}

/** visibilityState is a getter on the prototype, so it is shadowed on the document. */
export function setVisibility(state: "visible" | "hidden") {
  Object.defineProperty(document, "visibilityState", {
    value: state,
    configurable: true,
  });
}

/** The element the feed scrolls in. */
export const feed = (wrapper: VueWrapper) =>
  wrapper.find(".chat-feed__list").element as HTMLElement;

/** The state every test starts from: a reader, a correspondence, no messages. */
export function resetChatView() {
  vi.clearAllMocks();
  // Queued one-shot answers outlive clearAllMocks and would be served to the next test.
  api.getMessages.mockReset();
  hub.handlers.clear();
  hub.connected.value = false;
  // A test that hides the tab shadows this on the document, and the shadow outlives it.
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
}

/** One page as the API serves it, oldest first; `hasNext` puts the reader in the middle. */
export function messagePage(ids: string[], { hasNext = false } = {}) {
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

export const shownIds = () => useMessagingStore().messagesList.map((m) => m.id);
